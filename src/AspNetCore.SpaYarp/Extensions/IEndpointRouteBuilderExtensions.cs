using AspNetCore.SpaYarp;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Yarp.ReverseProxy.Forwarder;

namespace Microsoft.AspNetCore.Builder;

public static class IEndpointRouteBuilderExtensions
{

    /// <summary>
    /// Adds a "catch-all" route endpoint to the <see cref="IEndpointRouteBuilder"/> that forwards all requests to the SPA dev server.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route endpoint to.</param>
    /// <returns>The <see cref="IEndpointRouteBuilder"/>.</returns>
    [Obsolete("This method doesn't do anything anymore, and can be simply removed.")]
    public static IEndpointRouteBuilder MapSpaYarp(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }

    [Obsolete($"Please use {nameof(MapSpaYarpForwarder)} instead.")]
    public static IEndpointRouteBuilder MapSpaYarp(this IEndpointRouteBuilder endpoints, string publicPath,
        string clientUrl, string? policyName = null)
    {
        return MapSpaYarpForwarder(endpoints, publicPath, clientUrl, policyName);
    }

    /// <summary>
    /// Adds direct forwarding of HTTP requests that match the pattern specified in <typeparamref name="TOptions"/>.PublicPath to a SPA dev server.
    /// It is expected that the SPA dev server will be started manually.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route endpoint to.</param>
    /// <param name="publicPath">The public path of the endpoint</param>
    /// <param name="clientUrl">The Url of the dev server to proxy to</param>
    /// <param name="policyName">The auth policy name to add to the mapping</param>
    /// <returns>The <see cref="IEndpointRouteBuilder"/>.</returns>
    public static IEndpointRouteBuilder MapSpaYarpForwarder(this IEndpointRouteBuilder endpoints, string publicPath,
        string clientUrl, string? policyName = null)
    {
        var spaProxyLaunchManager = endpoints.ServiceProvider.GetService<SpaProxyLaunchManager<SpaDevelopmentServerOptions>>();

        if (spaProxyLaunchManager == null)
        {
            return endpoints;
        }

        // Configure our own HttpMessageInvoker for outbound calls for proxy operations
        var httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false
        });

        var transformer = new CustomTransformer();
        var requestOptions = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(100) };

        var point = endpoints.MapForwarder(string.Format("{0}/{{**catch-all}}", publicPath), clientUrl, requestOptions, transformer, httpClient);

        if (policyName is not null)
        {
            point.RequireAuthorization(policyName);
        }

        return endpoints;
    }
}

