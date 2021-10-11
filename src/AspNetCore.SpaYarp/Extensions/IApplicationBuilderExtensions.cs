using AspNetCore.SpaYarp;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder;

public static class IApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the middleware for the SPA proxy to the application's request pipeline.
    /// The middleware gets only added if the 'spa.proxy.json' file exists and the SpaYarp services were added (there is a check for the SpaProxyLaunchManager).
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> instance used to configure the request pipeline.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseSpaYarpMiddleware(this IApplicationBuilder app)
    {
        var spaProxyLaunchManager = app.ApplicationServices.GetService<SpaProxyLaunchManager>();

        if (spaProxyLaunchManager == null)
        {
            return app;
        }

        app.UseMiddleware<SpaProxyMiddleware>();

        return app;
    }
}

