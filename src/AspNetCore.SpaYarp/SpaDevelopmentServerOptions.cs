// based on https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/Spa/SpaProxy/src/SpaDevelopmentServerOptions.cs
namespace AspNetCore.SpaYarp;

public class SpaDevelopmentServerOptions
{
    public string ClientUrl { get; set; } = "";

    public string LaunchCommand { get; set; } = "";

    public int MaxTimeoutInSeconds { get; set; }

    public TimeSpan MaxTimeout => TimeSpan.FromSeconds(MaxTimeoutInSeconds);

    public string WorkingDirectory { get; set; } = "";

    public string PublicPath { get; set; } = "";
}