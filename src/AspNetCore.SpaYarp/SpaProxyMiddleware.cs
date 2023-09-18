// based on https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/Spa/SpaProxy/src/SpaProxyMiddleware.cs
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Text;
using Yarp.ReverseProxy.Forwarder;

namespace AspNetCore.SpaYarp;
/// <summary>
/// Middleware to display a page while the SPA proxy is launching and redirect to the proxy url once the proxy is
/// ready or we have given up trying to start it.
/// This is to help Visual Studio work well in several scenarios by allowing VS to:
/// 1) Launch on the URL configured for the backend (we handle the redirect to the proxy when ready).
/// 2) Ensure that the server is up and running quickly instead of waiting for the proxy to be ready to start the
///    server which causes Visual Studio to think the app failed to launch.
/// </summary>
public class SpaProxyMiddleware<TOptions>
    where TOptions : SpaDevelopmentServerOptions
{
    private static bool _spaClientRunning = false;

    private static CustomTransformer _transformer = new CustomTransformer();
    private static ForwarderRequestConfig _requestOptions = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(100) };
    private static HttpMessageInvoker _httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false
    });

    private readonly IHttpForwarder _forwarder;

    private readonly SpaProxyLaunchManager<TOptions> _spaProxyLaunchManager;
    private readonly IOptions<TOptions> _options;
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly ILogger<SpaProxyMiddleware<TOptions>> _logger;

    public SpaProxyMiddleware(
        SpaProxyLaunchManager<TOptions> spaProxyLaunchManager,
        IOptions<TOptions> options,
        IHostApplicationLifetime hostLifetime,
        IHttpForwarder forwarder,
        ILogger<SpaProxyMiddleware<TOptions>> logger)
    {
        _spaProxyLaunchManager = spaProxyLaunchManager ?? throw new ArgumentNullException(nameof(spaProxyLaunchManager));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _hostLifetime = hostLifetime ?? throw new ArgumentNullException(nameof(hostLifetime));
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(hostLifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Invoke(HttpContext context)
    {
        if (!_spaClientRunning && !await _spaProxyLaunchManager.IsSpaClientRunning(context.RequestAborted))
        {
            _spaProxyLaunchManager.StartInBackground(_hostLifetime.ApplicationStopping);
            _logger.LogInformation("SPA client is not ready. Returning temporary landing page.");
            context.Response.Headers[HeaderNames.CacheControl] = "no-cache, no-store, must-revalidate, max-age=0";
            context.Response.ContentType = "text/html";

            await using var writer = new StreamWriter(context.Response.Body, Encoding.UTF8);
            await writer.WriteAsync(GenerateSpaLaunchPage(_options.Value));
        }
        else
        {
            _logger.LogInformation($"SPA client is ready.");
            _spaClientRunning = true;

            await _forwarder.SendAsync(context, _options.Value.ClientUrl, _httpClient, _requestOptions, _transformer);
        }

        string GenerateSpaLaunchPage(SpaDevelopmentServerOptions options)
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset = ""UTF-8"" >
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
  <meta http-equiv=""refresh"" content=""3"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>SPA client launch page</title>
</head>
<body>
  <style>
    @media (prefers-color-scheme: dark) {{
      :root {{
        background: black;
        color: gray;
      }}
    }}
  </style>
  <h1>Launching the SPA client...</h1>
  <p>This page will automatically refresh when the SPA client is ready.</p>
</body>
</html>";
        }
    }
}