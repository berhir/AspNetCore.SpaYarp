using DotNetAngular;
using Microsoft.Extensions.Options;
using System.Net;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpForwarder();
builder.Services.AddSingleton<SpaProxyLaunchManager>();
builder.Services.AddOptions<SpaDevelopmentServerOptions>()
    .Configure(options =>
    {
        options.ClientUrl = "https://localhost:44478";
        options.LaunchCommand = "npm start";
        options.WorkingDirectory = @"C:\git\DotNetAngular\ClientApp";
        options.MaxTimeoutInSeconds = 120;
    });

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

if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<SpaProxyMiddleware>();

    // proxy config
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

    // Setup our own request transform class
    var transformer = HttpTransformer.Default;
    var requestOptions = new ForwarderRequestConfig { Timeout = TimeSpan.FromSeconds(100) };

    app.UseEndpoints(endpoints =>
    {
        // When using IHttpForwarder for direct forwarding you are responsible for routing, destination discovery, load balancing, affinity, etc..
        // For an alternate example that includes those features see BasicYarpSample.
        endpoints.Map("/{**catch-all}", async httpContext =>
        {
            var error = await forwarder.SendAsync(httpContext, spaOptions.ClientUrl, httpClient, requestOptions, transformer);
            // Check if the proxy operation was successful
            if (error != ForwarderError.None)
            {
                var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
                var exception = errorFeature?.Exception;
            }
        });
    });
}
else
{
    app.MapFallbackToFile("index.html"); ;
}

app.Run();
