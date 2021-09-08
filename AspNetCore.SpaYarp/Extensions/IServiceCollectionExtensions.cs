using AspNetCore.SpaYarp;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds required services and configuration to use the SPA proxy.
    /// The services get only added if a "spa.proxy.json" file exists.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static void AddSpaYarp(this IServiceCollection services)
    {
        var spaProxyConfigFile = Path.Combine(AppContext.BaseDirectory, "spa.proxy.json");
        if (File.Exists(spaProxyConfigFile))
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(spaProxyConfigFile)
                .Build();

            services.AddHttpForwarder();
            services.AddSingleton<SpaProxyLaunchManager>();
            services.Configure<SpaDevelopmentServerOptions>(configuration.GetSection("SpaProxyServer"));
        }
    }
}

