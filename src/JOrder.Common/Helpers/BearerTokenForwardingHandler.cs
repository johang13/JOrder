using Microsoft.AspNetCore.Http;

namespace JOrder.Common.Helpers;

/// <summary>
/// A <see cref="DelegatingHandler"/> that copies the incoming
/// <c>Authorization: Bearer</c> header to every outbound <see cref="HttpClient"/> request.
/// Attach to typed clients via <c>builder.AddHttpMessageHandler&lt;BearerTokenForwardingHandler&gt;()</c>
/// or the <c>.WithBearerForwarding()</c> extension from <c>JOrder.Common</c>.
/// </summary>
public sealed class BearerTokenForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();

        if (!string.IsNullOrEmpty(authHeader)
            && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            && !request.Headers.Contains("Authorization"))
            request.Headers.TryAddWithoutValidation("Authorization", authHeader);

        return base.SendAsync(request, cancellationToken);
    }
}