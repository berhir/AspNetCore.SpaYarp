# AspNetCore.SpaYarpProxy
This sample is based on the ideas and code of the [ASP.NET Core SPA templates](https://github.com/dotnet/spa-templates).  
But it uses a slightly different approach. Instead of using the SPA dev server to forward requests to the host/backend, it uses [YARP](https://microsoft.github.io/reverse-proxy/index.html) to forward all requests that can't be handled by the host to the client application.

This project is a proof of concept. Let me know if you find any issues with this approach.

## Running the sample

To run the sample, open the solution in Visual Studio and start the `AspNetAngularSpaYarp` project.
It reuses an existing SPA dev server if the client app is already running (started manually in a terminal or VS Code) or it starts a new one.

## Debugging

It's possible to debug the .NET code and the SPA at the same time with different editors. To use Visual Studio Code to debug the SPA, create a `launch.json` file as [described in the docs](https://code.visualstudio.com/docs/nodejs/angular-tutorial#_debugging-angular).
But instead of the URL of the SPA, the URL of the .NET host must be used.  

This is the `launch.json` used in this sample:
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "type": "pwa-msedge",
      "request": "launch",
      "name": "Launch Edge against localhost",
      "url": "https://localhost:7113",
      "webRoot": "${workspaceFolder}"
    }
  ]
}
```

## Background

### The new experience for SPA templates
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

### Using YARP

This sample uses [YARP](https://microsoft.github.io/reverse-proxy/index.html) to forward all requests that can't be handled by the host to the client application. It reuses a lot of code from the [Microsoft.AspNetCore.SpaProxy](https://github.com/dotnet/aspnetcore/tree/main/src/Middleware/Spa/SpaProxy/src) project that you can find on GitHub.

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
