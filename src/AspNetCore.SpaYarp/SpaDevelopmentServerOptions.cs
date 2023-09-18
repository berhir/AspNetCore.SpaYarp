// based on https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/Spa/SpaProxy/src/SpaDevelopmentServerOptions.cs
namespace AspNetCore.SpaYarp;

/// <summary>
/// Options that are taken from 'spa.proxy.json' file as-is.
/// </summary>
public class SpaDevelopmentServerOptions
{
    public virtual string ClientUrl { get; set; } = "";

    public virtual string LaunchCommand { get; set; } = "";

    public virtual int MaxTimeoutInSeconds { get; set; }

    public TimeSpan MaxTimeout => TimeSpan.FromSeconds(MaxTimeoutInSeconds);

    public virtual string WorkingDirectory { get; set; } = "";

    public virtual string PublicPath { get; set; } = "";
}