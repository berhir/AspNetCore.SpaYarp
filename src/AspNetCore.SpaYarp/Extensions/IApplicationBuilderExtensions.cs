using AspNetCore.SpaYarp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Builder;

public static class IApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the middleware for the SPA proxy to the application's request pipeline.
    /// The middleware gets only added if the 'spa.proxy.json' file exists and the SpaYarp services were added (there is a check for the SpaProxyLaunchManager).
    /// Middleware will be configured exactly as specified in 'spa.proxy.json'.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> instance used to configure the request pipeline.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseSpaYarpMiddleware(this IApplicationBuilder app)
    {
        return UseSpaYarpMiddleware<SpaDevelopmentServerOptions>(app);
    }

    /// <summary>
    /// Adds the middleware for the SPA proxy to the application's request pipeline.
    /// The middleware gets only added if the 'spa.proxy.json' file exists and the SpaYarp services were added (there is a check for the SpaProxyLaunchManager).
    /// Middleware will be configured based on configuration from 'spa.proxy.json', but configuration from <typeparamref name="TOptions"/> will take precedence.
    /// </summary>
    /// <typeparam name="TOptions">Options class that inherits from <see cref="SpaDevelopmentServerOptions"/> and can override its values.</typeparam>
    /// <param name="app">The <see cref="IApplicationBuilder"/> instance used to configure the request pipeline.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseSpaYarpMiddleware<TOptions>(this IApplicationBuilder app)
        where TOptions : SpaDevelopmentServerOptions
    {
        var spaProxyLaunchManager = app.ApplicationServices.GetService<SpaProxyLaunchManager<TOptions>>();

        if (spaProxyLaunchManager == null)
        {
            return app;
        }

        app.UseMiddleware<SpaProxyMiddleware<TOptions>>();

        return app;
    }

    /// <summary>
    /// Adds the middleware for the SPA proxy to the branch of application's request pipeline, based on pattern specified in <typeparamref name="TOptions"/>.PublicPath.
    /// The middleware gets only added if the 'spa.proxy.json' file exists and the SpaYarp services were added.
    /// Middleware will be configured based on configuration from 'spa.proxy.json', but configuration from <typeparamref name="TOptions"/> will take precedence.
    /// </summary>
    /// <typeparam name="TOptions">Options class that inherits from <see cref="SpaDevelopmentServerOptions"/> and can override its values.</typeparam>
    /// <param name="app">The <see cref="IApplicationBuilder"/> instance used to configure the request pipeline.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder MapSpaYarp<TOptions>(this IApplicationBuilder app, bool preserveMatchedPathSegment = false)
        where TOptions : SpaDevelopmentServerOptions
    {
        var options = app.ApplicationServices.GetService<IOptions<TOptions>>();

        if (options == null)
        {
            return app;
        }

        if (string.IsNullOrEmpty(options.Value.PublicPath))
        {
            throw new ArgumentException($"Use '{nameof(UseSpaYarpMiddleware)}' with empty '{typeof(TOptions).Name}.{nameof(SpaDevelopmentServerOptions.PublicPath)}'");
        }

        app.Map($"/{options.Value.PublicPath}", preserveMatchedPathSegment, app =>
        {
            app.UseMiddleware<SpaProxyMiddleware<TOptions>>();
        });

        return app;
    }
}

