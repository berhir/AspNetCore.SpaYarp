namespace Microsoft.AspNetCore.Builder;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Adds the middleware for the SPA proxy to the application's request pipeline
    /// and adds a "catch-all" route endpoint that forwards all requests to the SPA dev server.
    /// The middleware and route endpoint get only added if the 'spa.proxy.json' file exists and the SpaYarp services were added (there is a check for the SpaProxyLaunchManager).
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> used to configure the HTTP pipeline, and routes.</param>
    /// <returns>The <see cref="WebApplication"/>.</returns>
    public static WebApplication UseSpaYarp(this WebApplication app)
    {
        app.UseSpaYarpMiddleware();
        app.MapSpaYarp();
        return app;
    }
}
