var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Like with Microsoft.AspNetCore.SpaProxy, a 'spa.proxy.json' file gets generated based on the values in the project file (SpaRoot, SpaClientUrl, SpaLaunchCommand).
// This file gets not published when using "dotnet publish".
// The services get not added and the proxy is not used when the file does not exist.
builder.Services.AddSpaYarp(addDefaultManager: false)
    .AddSpaYarp<DevServerOptionsOne>()
    .AddSpaYarp<DevServerOptionsTwo>();

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

app.MapRazorPages();

//The middleware and route endpoint get only added if the 'spa.proxy.json' file exists and the SpaYarp services were added.
app.MapSpaYarp<DevServerOptionsOne>(preserveMatchedPathSegment: true);
app.MapSpaYarp<DevServerOptionsTwo>(preserveMatchedPathSegment: true);

// If the SPA proxy is used, this will never be reached.
app.MapFallbackToFile("index.html");

app.Run();

class DevServerOptionsOne : AspNetCore.SpaYarp.SpaDevelopmentServerOptions
{
    public override string WorkingDirectory => Path.Combine(base.WorkingDirectory, "ClientAppOne");
    public override string PublicPath => "one";
    public override string ClientUrl => "https://localhost:44478";
}

class DevServerOptionsTwo : AspNetCore.SpaYarp.SpaDevelopmentServerOptions
{
    public override string WorkingDirectory => Path.Combine(base.WorkingDirectory, "ClientAppTwo");
    public override string PublicPath => "two";
    public override string ClientUrl => "https://localhost:44479";
}