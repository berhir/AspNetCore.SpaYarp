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
