using JOrder.Common.Extensions;
using JOrder.Common.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JOrder.Common.UnitTests.Extensions;

public class WebApplicationExtensionsUnitTests
{
    [Fact]
    public async Task RunWarmupTasksAsync_ExecutesRegisteredTasksInOrder()
    {
        var executionOrder = new List<string>();

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(executionOrder);
        builder.Services.AddScoped<IJOrderWarmupTask, FirstWarmupTask>();
        builder.Services.AddScoped<IJOrderWarmupTask, SecondWarmupTask>();
        var app = builder.Build();

        await app.RunWarmupTasksAsync();

        Assert.Equal(["First", "Second"], executionOrder);
    }

    [Fact]
    public async Task RunWarmupTasksAsync_WithNoTasks_CompletesSuccessfully()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        await app.RunWarmupTasksAsync();
    }

    [Fact]
    public async Task RunWarmupTasksAsync_PassesCancellationTokenToTasks()
    {
        var tokenCapture = new TokenCaptureWarmupTask();
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IJOrderWarmupTask>(tokenCapture);
        var app = builder.Build();
        using var cts = new CancellationTokenSource();

        await app.RunWarmupTasksAsync(cts.Token);

        Assert.Equal(cts.Token, tokenCapture.ReceivedToken);
    }

    [Fact]
    public void MapDefaultEndpoints_WithHealthProbes_MapsProbeEndpoints()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Services.AddControllers();
        builder.Services.AddRateLimiter(_ => { });
        builder.Services.AddSingleton<IHealthProbes, StubHealthProbes>();
        var app = builder.Build();

        app.MapDefaultEndpoints();

        var routePatterns = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText)
            .ToArray();

        Assert.Contains("/startupz", routePatterns);
        Assert.Contains("/readyz", routePatterns);
        Assert.Contains("/livez", routePatterns);
    }

    [Fact]
    public async Task MapDefaultEndpoints_WithHealthProbes_InvokesProbeDelegates()
    {
        var probes = new CountingHealthProbes();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Services.AddControllers();
        builder.Services.AddRateLimiter(_ => { });
        builder.Services.AddSingleton<IHealthProbes>(probes);
        var app = builder.Build();

        app.MapDefaultEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is "/startupz" or "/readyz" or "/livez")
            .ToArray();

        Assert.Equal(3, endpoints.Length);

        foreach (var endpoint in endpoints)
        {
            var context = new DefaultHttpContext { RequestServices = app.Services };
            context.Response.Body = new MemoryStream();

            Assert.NotNull(endpoint.RequestDelegate);
            await endpoint.RequestDelegate!(context);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        Assert.Equal(1, probes.StartupCalls);
        Assert.Equal(1, probes.ReadinessCalls);
        Assert.Equal(1, probes.LivenessCalls);
    }

    [Fact]
    public void MapDefaultEndpoints_WithoutHealthProbes_DoesNotMapProbeEndpoints()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Services.AddControllers();
        builder.Services.AddRateLimiter(_ => { });
        var app = builder.Build();

        app.MapDefaultEndpoints();

        var routePatterns = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText)
            .ToArray();

        Assert.DoesNotContain("/startupz", routePatterns);
        Assert.DoesNotContain("/readyz", routePatterns);
        Assert.DoesNotContain("/livez", routePatterns);
    }

    private sealed class FirstWarmupTask(List<string> order) : IJOrderWarmupTask
    {
        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            order.Add("First");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondWarmupTask(List<string> order) : IJOrderWarmupTask
    {
        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            order.Add("Second");
            return Task.CompletedTask;
        }
    }

    private sealed class TokenCaptureWarmupTask : IJOrderWarmupTask
    {
        public CancellationToken ReceivedToken { get; private set; }

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class StubHealthProbes : IHealthProbes
    {
        public Task StartupProbe() => Task.CompletedTask;
        public Task ReadinessProbe() => Task.CompletedTask;
        public Task LivenessProbe() => Task.CompletedTask;
    }

    private sealed class CountingHealthProbes : IHealthProbes
    {
        public int StartupCalls { get; private set; }
        public int ReadinessCalls { get; private set; }
        public int LivenessCalls { get; private set; }

        public Task StartupProbe()
        {
            StartupCalls++;
            return Task.CompletedTask;
        }

        public Task ReadinessProbe()
        {
            ReadinessCalls++;
            return Task.CompletedTask;
        }

        public Task LivenessProbe()
        {
            LivenessCalls++;
            return Task.CompletedTask;
        }
    }
}
