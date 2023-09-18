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
    public static (IServiceCollection services, IConfiguration? configuration) AddSpaYarp(this IServiceCollection services, bool addDefaultManager = true)
    {
        var spaProxyConfigFile = Path.Combine(AppContext.BaseDirectory, "spa.proxy.json");
        if (File.Exists(spaProxyConfigFile))
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(spaProxyConfigFile)
                .Build();

            services.AddHttpForwarder();
            services.Configure<SpaDevelopmentServerOptions>(configuration.GetSection("SpaProxyServer"));

            if (addDefaultManager)
            {
                services.AddSingleton<SpaProxyLaunchManager<SpaDevelopmentServerOptions>>();
            }

            return (services, configuration);
        }

        return (services, null);
    }

    /// <summary>
    /// Adds required services and configuration to use the SPA proxy.
    /// The services get only added if a "spa.proxy.json" file exists.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static (IServiceCollection services, IConfiguration? configuration) AddSpaYarp<T>(this (IServiceCollection services, IConfiguration? configuration) services) where T : SpaDevelopmentServerOptions, new()
    {
        if (services.configuration != null)
        {
            services.services.AddSingleton<SpaProxyLaunchManager<T>>();
            services.services.Configure<T>(services.configuration.GetSection("SpaProxyServer"));
        }

        return services;
    }
}

