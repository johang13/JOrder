using JOrder.Common.Extensions;
using JOrder.Common.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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
}
