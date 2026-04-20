using System.Net;
using JOrder.Common.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace JOrder.Common.UnitTests.Middleware;

public class RequestOriginLoggingMiddlewareUnitTests
{
    private readonly ILogger<RequestOriginLoggingMiddleware> _logger = Substitute.For<ILogger<RequestOriginLoggingMiddleware>>();

    private RequestOriginLoggingMiddleware CreateMiddleware(RequestDelegate next)
        => new(next, _logger);

    private static DefaultHttpContext CreateHttpContext(string method = "GET", string path = "/api/test")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        return context;
    }

    [Fact]
    public async Task InvokeAsync_CallsNextDelegate()
    {
        var wasCalled = false;
        var middleware = CreateMiddleware(_ => { wasCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(CreateHttpContext());

        Assert.True(wasCalled);
    }

    [Fact]
    public async Task InvokeAsync_LogsRequestInformation()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(CreateHttpContext("POST", "/api/orders"));

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("POST") && v.ToString()!.Contains("/api/orders")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData("/startupz")]
    [InlineData("/readyz")]
    [InlineData("/livez")]
    public async Task InvokeAsync_SkipsLogging_ForHealthProbePaths(string path)
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(CreateHttpContext(path: path));

        _logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvokeAsync_LogsForwardedHeaders_WhenPresent()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "10.0.0.1";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers.Origin = "https://example.com";

        await middleware.InvokeAsync(context);

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v =>
                v.ToString()!.Contains("10.0.0.1") &&
                v.ToString()!.Contains("https") &&
                v.ToString()!.Contains("https://example.com")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvokeAsync_StillLogs_WhenNextThrows()
    {
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(CreateHttpContext()));

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvokeAsync_LogsElapsedTime()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(CreateHttpContext());

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("ms")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}