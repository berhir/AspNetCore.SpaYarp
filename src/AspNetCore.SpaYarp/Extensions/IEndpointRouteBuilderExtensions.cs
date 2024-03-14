﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using Yarp.ReverseProxy.Forwarder;

namespace AspNetCore.SpaYarp.Extensions;

public static class IEndpointRouteBuilderExtensions
{

    /// <summary>
    /// Adds a "catch-all" route endpoint to the <see cref="IEndpointRouteBuilder"/> that forwards all requests to the SPA dev server.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route endpoint to.</param>
    /// <returns>The <see cref="IEndpointConventionBuilder"/>.</returns>
    public static IEndpointConventionBuilder? MapSpaYarp(this IEndpointRouteBuilder endpoints)
    {
        var spaOptions = endpoints.ServiceProvider.GetRequiredService<IOptions<SpaDevelopmentServerOptions>>().Value;
        return endpoints.MapSpaYarp(spaOptions.PublicPath, spaOptions.ClientUrl);
    }

    /// <summary>
    /// Adds a "catch-all" route endpoint to the <see cref="IEndpointRouteBuilder"/> that forwards all requests to the SPA dev server.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route endpoint to.</param>
    /// <param name="publicPath">The public path of the endpoint</param>
    /// <param name="clientUrl">The Url of the dev server to proxy to</param>
    /// <param name="policyName">The auth policy name to add to the mapping</param>
    /// <returns>The <see cref="IEndpointRouteBuilder"/>.</returns>
    public static IEndpointConventionBuilder? MapSpaYarp(this IEndpointRouteBuilder endpoints, string publicPath, string clientUrl)
    {
        var spaProxyLaunchManager = endpoints.ServiceProvider.GetService<SpaProxyLaunchManager>();

        if (spaProxyLaunchManager == null)
        {
            return null;
        }

        // configure the proxy
        var forwarder = endpoints.ServiceProvider.GetRequiredService<IHttpForwarder>();

        // Configure our own HttpMessageInvoker for outbound calls for proxy operations
        var httpClient = new HttpMessageInvoker(new SocketsHttpHandler() {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false
        });

        var transformer = new CustomTransformer();
        var requestOptions = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(100) };

        var point = endpoints.Map(string.Format("{0}/{{**catch-all}}", publicPath), async httpContext =>
        {
            var error = await forwarder.SendAsync(httpContext, clientUrl, httpClient, requestOptions, transformer);
            // Check if the proxy operation was successful
            if (error != ForwarderError.None)
            {
                var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
                var exception = errorFeature?.Exception;
            }
        });
        return point;
    }

    /// <summary>
    /// Custom request transformation
    /// </summary>
    private class CustomTransformer : HttpTransformer
    {
        /// <summary>
        /// A callback that is invoked prior to sending the proxied request. All HttpRequestMessage
        /// fields are initialized except RequestUri, which will be initialized after the
        /// callback if no value is provided. The string parameter represents the destination
        /// URI prefix that should be used when constructing the RequestUri. The headers
        /// are copied by the base implementation, excluding some protocol headers like HTTP/2
        /// pseudo headers (":authority").
        /// </summary>
        /// <param name="httpContext">The incoming request.</param>
        /// <param name="proxyRequest">The outgoing proxy request.</param>
        /// <param name="destinationPrefix">The uri prefix for the selected destination server which can be used to create
        /// the RequestUri.</param>
        public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix, CancellationToken cancellationToken)
        {
            // Copy all request headers
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

            // Suppress the original request header, use the one from the destination Uri.
            proxyRequest.Headers.Host = null;
        }
    }
}

