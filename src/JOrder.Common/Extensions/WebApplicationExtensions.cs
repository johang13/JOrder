using System.Diagnostics;
using JOrder.Common.Helpers;
using JOrder.Common.Middleware;
using JOrder.Common.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;

namespace JOrder.Common.Extensions;

/// <summary>
/// Provides extension methods for configuring ASP.NET Core <see cref="WebApplication"/> instances.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures the middleware pipeline with HTTPS redirection, controller routing,
    /// and conditionally authentication and authorisation (only if those services are registered).
    /// In the Development environment, also maps the OpenAPI endpoint and Scalar UI,
    /// and enables the developer exception page.
    /// Logs all discovered controllers from the calling assembly.
    /// </summary>
    /// <param name="webApplication">The web application to configure.</param>
    /// <returns>The same <see cref="WebApplication"/> instance for chaining.</returns>
    public static WebApplication MapDefaultEndpoints(this WebApplication webApplication)
    {
        if (webApplication.Environment.IsDevelopment())
        {
            webApplication.MapOpenApi();
            webApplication.MapScalarApiReference();
            webApplication.UseDeveloperExceptionPage();
        }
        
        // Trust X-Forwarded-* from ingress (scheme/remote IP)
        webApplication.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        webApplication.UseMiddleware<RequestOriginLoggingMiddleware>();

        // UseRateLimiter is safe to call unconditionally — it's a no-op when
        // no global limiter or policies have been registered via AddRateLimiter.
        webApplication.UseRateLimiter();

        var runningInContainer = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!runningInContainer)
        {
            webApplication.UseHttpsRedirection();
        }

        if (webApplication.Services.GetService<IAuthenticationSchemeProvider>() is not null)
            webApplication.UseAuthentication();

        if (webApplication.Services.GetService<IAuthorizationHandlerProvider>() is not null)
            webApplication.UseAuthorization();

        webApplication.MapControllers();
        
        MapHealthProbes(webApplication);
        
        ControllerLoggingHelper.LogMappedControllers(webApplication);

        return webApplication;
    }

    private static void MapHealthProbes(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        if (scope.ServiceProvider.GetService<IHealthProbes>() is null)
        {
            app.Logger.LogInformation("No health probes registered.");
            return;
        }

        var probes = app.MapGroup("")
            .AllowAnonymous()
            .ExcludeFromDescription();

        probes.MapGet("/startupz", async (IHealthProbes hp) =>
        {
            await hp.StartupProbe();
            return Results.Ok("started");
        });

        probes.MapGet("/readyz", async (IHealthProbes hp) =>
        {
            await hp.ReadinessProbe();
            return Results.Ok("ready");
        });

        probes.MapGet("/livez", async (IHealthProbes hp) =>
        {
            await hp.LivenessProbe();
            return Results.Ok("alive");
        });

        app.Logger.LogInformation(string.Join(Environment.NewLine,
            string.Empty,
            "Health probes:",
            "    ├─ GET /startupz",
            "    ├─ GET /readyz",
            "    └─ GET /livez",
            string.Empty));
    }
    
    /// <summary>
    /// Resolves and executes all registered <see cref="IJOrderWarmupTask"/> implementations
    /// sequentially before the application starts accepting traffic.
    /// Each task is logged by name with execution time. If no tasks are registered, a single informational log is emitted.
    /// </summary>
    /// <param name="webApplication">The application instance.</param>
    /// <param name="cancellationToken">Optional cancellation token forwarded to each task.</param>
    public static async Task RunWarmupTasksAsync(
        this WebApplication webApplication,
        CancellationToken cancellationToken = default)
    {
        using var scope = webApplication.Services.CreateScope();
        var warmupTasks = scope.ServiceProvider.GetServices<IJOrderWarmupTask>().ToList();

        if (warmupTasks.Count == 0)
        {
            webApplication.Logger.LogInformation("No warmup tasks registered.");
            return;
        }

        foreach (var warmupTask in warmupTasks)
        {
            var taskName = warmupTask.GetType().Name;
            webApplication.Logger.LogInformation("Running warmup task {WarmupTaskName}.", taskName);
            
            var stopwatch = Stopwatch.StartNew();
            await warmupTask.ExecuteAsync(cancellationToken);
            stopwatch.Stop();
            
            webApplication.Logger.LogInformation(
                "Completed warmup task {WarmupTaskName} in {ExecutionTimeMs}ms.",
                taskName,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}