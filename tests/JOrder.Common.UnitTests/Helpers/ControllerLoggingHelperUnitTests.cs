using System.Reflection;
using JOrder.Common.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JOrder.Common.UnitTests.Helpers;

public class ControllerLoggingHelperUnitTests
{
    [Fact]
    public void LogMappedControllers_WhenAssemblyIsNull_LogsWarning()
    {
        var sink = new ListLoggerProvider();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(sink);
        var app = builder.Build();

        ControllerLoggingHelper.LogMappedControllers(app, entryAssembly: null);

        Assert.Contains(sink.Messages, m => m.Level == LogLevel.Warning && m.Message.Contains("Could not determine entry assembly", StringComparison.Ordinal));
    }

    [Fact]
    public void LogMappedControllers_WhenNoControllers_LogsInformation()
    {
        var sink = new ListLoggerProvider();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(sink);
        var app = builder.Build();

        ControllerLoggingHelper.LogMappedControllers(app, typeof(string).Assembly);

        Assert.Contains(sink.Messages, m => m.Level == LogLevel.Information && m.Message.Contains("No controllers mapped.", StringComparison.Ordinal));
    }

    [Fact]
    public void LogMappedControllers_WithControllers_LogsResolvedRoutes()
    {
        var sink = new ListLoggerProvider();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(sink);
        var app = builder.Build();

        ControllerLoggingHelper.LogMappedControllers(app, Assembly.GetExecutingAssembly());

        var mappedControllersLog = Assert.Single(
            sink.Messages,
            m => m.Level == LogLevel.Information && m.Message.Contains("Mapped ", StringComparison.Ordinal));
        Assert.Contains("OrdersController", mappedControllersLog.Message, StringComparison.Ordinal);
        Assert.Contains("GET api/Orders", mappedControllersLog.Message, StringComparison.Ordinal);
        Assert.Contains("POST api/Orders/items", mappedControllersLog.Message, StringComparison.Ordinal);
        Assert.Contains("RootController", mappedControllersLog.Message, StringComparison.Ordinal);
        Assert.Contains("GET /", mappedControllersLog.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LogMappedControllers_ControllerWithoutControllerSuffix_ReplacesControllerToken()
    {
        var sink = new ListLoggerProvider();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(sink);
        var app = builder.Build();

        ControllerLoggingHelper.LogMappedControllers(app, Assembly.GetExecutingAssembly());

        var mappedControllersLog = Assert.Single(
            sink.Messages,
            m => m.Level == LogLevel.Information && m.Message.Contains("Mapped ", StringComparison.Ordinal));

        Assert.Contains("Plain", mappedControllersLog.Message, StringComparison.Ordinal);
        Assert.Contains("GET x/Plain", mappedControllersLog.Message, StringComparison.Ordinal);
    }

    [Route("api/[controller]")]
    private sealed class OrdersController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok();

        [HttpPost("items")]
        public IActionResult Create() => Ok();
    }

    [Route("")]
    private sealed class RootController : ControllerBase
    {
        [HttpGet("")]
        public IActionResult Get() => Ok();
    }

    [Route("x/[controller]")]
    private sealed class Plain : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok();
    }

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        public List<LogEntry> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new ListLogger(Messages);

        public void Dispose()
        {
        }
    }

    private sealed class ListLogger(List<LogEntry> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
