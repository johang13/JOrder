using JOrder.Common.Helpers;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace JOrder.Common.UnitTests.Helpers;

public class BearerTokenForwardingHandlerUnitTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly BearerTokenForwardingHandler _handler;
    private readonly CapturingInnerHandler _innerHandler;

    public BearerTokenForwardingHandlerUnitTests()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _innerHandler = new CapturingInnerHandler();
        _handler = new BearerTokenForwardingHandler(_httpContextAccessor)
        {
            InnerHandler = _innerHandler
        };
    }

    [Fact]
    public async Task SendAsync_WithBearerToken_ForwardsAuthorizationHeader()
    {
        // Arrange
        const string token = "Bearer eyJhbGciOiJSUzI1NiJ9.test.sig";
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = token;
        _httpContextAccessor.HttpContext.Returns(httpContext);

        var outboundRequest = new HttpRequestMessage(HttpMethod.Get, "https://catalogue.jorder.localhost/products");

        // Act
        await new HttpMessageInvoker(_handler).SendAsync(outboundRequest, CancellationToken.None);

        // Assert
        Assert.True(_innerHandler.LastRequest!.Headers.TryGetValues("Authorization", out var values));
        Assert.Equal(token, values.Single());
    }

    [Fact]
    public async Task SendAsync_WithNoIncomingToken_DoesNotAddAuthorizationHeader()
    {
        // Arrange
        var httpContext = new DefaultHttpContext(); // no Authorization header
        _httpContextAccessor.HttpContext.Returns(httpContext);

        var outboundRequest = new HttpRequestMessage(HttpMethod.Get, "https://catalogue.jorder.localhost/products");

        // Act
        await new HttpMessageInvoker(_handler).SendAsync(outboundRequest, CancellationToken.None);

        // Assert
        Assert.False(_innerHandler.LastRequest!.Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task SendAsync_WithNullHttpContext_DoesNotAddAuthorizationHeader()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var outboundRequest = new HttpRequestMessage(HttpMethod.Get, "https://catalogue.jorder.localhost/products");

        // Act
        await new HttpMessageInvoker(_handler).SendAsync(outboundRequest, CancellationToken.None);

        // Assert
        Assert.False(_innerHandler.LastRequest!.Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task SendAsync_DoesNotOverwriteExistingAuthorizationHeader()
    {
        // Arrange — upstream already set a different token on the outbound request
        const string incomingToken = "Bearer incoming-token";
        const string existingToken = "Bearer pre-existing-token";

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = incomingToken;
        _httpContextAccessor.HttpContext.Returns(httpContext);

        var outboundRequest = new HttpRequestMessage(HttpMethod.Get, "https://catalogue.jorder.localhost/products");
        outboundRequest.Headers.TryAddWithoutValidation("Authorization", existingToken);

        // Act
        await new HttpMessageInvoker(_handler).SendAsync(outboundRequest, CancellationToken.None);

        // Assert — handler skips forwarding when Authorization is already set on the outbound request,
        // so the caller-supplied value is preserved and the incoming user token is not added.
        var headerValues = _innerHandler.LastRequest!.Headers.GetValues("Authorization").ToArray();
        Assert.Single(headerValues);
        Assert.Equal(existingToken, headerValues[0]);
    }

    /// <summary>
    /// Captures the last <see cref="HttpRequestMessage"/> passed to it without making a real HTTP call.
    /// </summary>
    private sealed class CapturingInnerHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}

