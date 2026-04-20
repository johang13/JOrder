using JOrder.Common.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace JOrder.Common.Extensions;

/// <summary>
/// Extension methods for <see cref="IHttpClientBuilder"/>.
/// </summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Attaches <see cref="BearerTokenForwardingHandler"/> to this <see cref="HttpClient"/>
    /// registration so the incoming user token is forwarded on every outbound request.
    /// Requires <see cref="HostApplicationExtensions.AddJOrderBearerForwarding"/> (or
    /// <see cref="HostApplicationExtensions.AddJOrderCommon"/> which calls it) to have been
    /// called first.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddHttpClient&lt;ICatalogueClient, CatalogueClient&gt;()
    ///     .WithBearerForwarding();
    /// </code>
    /// </example>
    public static IHttpClientBuilder WithBearerForwarding(this IHttpClientBuilder builder)
        => builder.AddHttpMessageHandler<BearerTokenForwardingHandler>();
}

