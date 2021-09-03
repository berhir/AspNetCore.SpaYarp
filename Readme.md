# AspNetCore.SpaYarpProxy
This is a sample that uses a different approach for developing SPAs as part of an ASP.NET Core application. It uses [YARP](https://microsoft.github.io/reverse-proxy/index.html) to forward all requests that can't be handled by the host to the client application.

It reuses an existing SPA dev server if the client app is already running (started manually in a terminal or VS Code) or it starts a new one.

Running the application works fine, but maybe there are scenarios that do not work. If you find something, please create an issue.

## The new experience for SPA templates
As discussed in https://github.com/dotnet/aspnetcore/issues/27887, a new experience for SPA templates in .NET 6 was introduced.
This new experience is completeley different to how the templates worked before. This is how it works now:
* Like before, there are additional settings in the project file (SpaRoot, SpaProxyServerUrl, SpaProxyLaunchCommand).
* An additional environment variable gets set in the launchSettings.json file (ASPNETCORE_HOSTINGSTARTUPASSEMBLIES).
* During the build a `spa.proxy.json` file gets generated.
* When the ASP.NET Core projects gets started and the ASPNETCORE_HOSTINGSTARTUPASSEMBLIES is set to "Microsoft.AspNetCore.SpaProxy", the proxy services and middleware get registred.
* The middleware checks if the SPA application is already running at the configured URL and starts it if it's not running.
* When the SPA application is running the user gets __redirected__ to the URL of the client application.
* Requests to the backend get handled by the SPA dev server and forwarded to the backend.

As mentioned by others in the GitHub issue, this has some drawbacks:
* It is different from production where the ASP.NET Core app serves the client application and no proxy is used for backend calls.
* The SPA frameworks must provide a dev server that supports proxying.
* We are limited to the features the dev server offers. New HTTP/3 features that are available as preview in .NET 6 or things like Windows authentication may not work.

## Using YARP

This sample uses [YARP](https://microsoft.github.io/reverse-proxy/index.html) to forward all requests that can't be handled by the host to the client application. It reuses a lot of code from the [Microsoft.AspNetCore.SpaProxy](https://github.com/dotnet/aspnetcore/tree/main/src/Middleware/Spa/SpaProxy/src) project that you can find on GitHub.

This project is a proof of concept and can be improved in many ways.

This is what the application startup looks like:

```cs
using AspNetCore.SpaYarpProxy;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// like with Microsoft.AspNetCore.SpaProxy, a 'spa.proxy.json' file gets generated based on the values in the project file (SpaRoot, SpaProxyClientUrl, SpaProxyLaunchCommand).
// this file gets not published when using "dotnet publish".
var spaProxyConfigFile = Path.Combine(AppContext.BaseDirectory, "spa.proxy.json");
var useSpaProxy = File.Exists(spaProxyConfigFile);
if (useSpaProxy)
{
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(spaProxyConfigFile)
        .Build();

    builder.Services.AddHttpForwarder();
    builder.Services.AddSingleton<SpaProxyLaunchManager>();
    builder.Services.Configure<SpaDevelopmentServerOptions>(configuration.GetSection("SpaProxyServer"));
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

if (useSpaProxy)
{
    app.UseSpaYarpProxy();
}
else
{
    app.MapFallbackToFile("index.html"); ;
}

app.Run();
```

The `UseSpaYarpProxy()` extension registers the `SpaProxyMiddleware` (that checks if the SPA is already running or starts it) and configures YARP to forward all unhandled requests.

```cs
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
```