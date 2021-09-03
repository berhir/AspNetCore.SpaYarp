using AspNetCore.SpaYarpProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using Yarp.ReverseProxy.Forwarder;

namespace Microsoft.AspNetCore.Builder;

public static class WebApplicationExtensions
{
    public static WebApplication UseSpaYarpProxy(this WebApplication app)
    {
        app.UseMiddleware<SpaProxyMiddleware>();

        // configure the proxy
        var forwarder = app.Services.GetRequiredService<IHttpForwarder>();
        var spaOptions = app.Services.GetRequiredService<IOptions<SpaDevelopmentServerOptions>>().Value;

        // Configure our own HttpMessageInvoker for outbound calls for proxy operations
        var httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false
        });

        var requestOptions = new ForwarderRequestConfig { Timeout = TimeSpan.FromSeconds(100) };

        app.Map("/{**catch-all}", async httpContext =>
        {
            var error = await forwarder.SendAsync(httpContext, spaOptions.ClientUrl, httpClient, requestOptions, HttpTransformer.Default);
            // Check if the proxy operation was successful
            if (error != ForwarderError.None)
            {
                var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
                var exception = errorFeature?.Exception;
            }
        });

        return app;
    }
}

