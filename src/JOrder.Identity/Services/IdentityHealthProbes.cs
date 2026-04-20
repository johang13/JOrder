using JOrder.Common.Attributes;
using JOrder.Common.Services.Interfaces;
using JOrder.Identity.Persistence;
using JOrder.Identity.Services.Interfaces;

namespace JOrder.Identity.Services;

[ScopedService]
public sealed class IdentityHealthProbes(
    ISigningKeyMaterialService signingKeyMaterialService,
    JOrderIdentityDbContext dbContext) : IHealthProbes
{
    /// <summary>
    /// Startup: verify DB is reachable and signing key material is loaded.
    /// Runs once at pod start before readiness/liveness kick in.
    /// </summary>
    public async Task StartupProbe()
    {
        // Eagerly access signing key to surface configuration errors at startup
        _ = signingKeyMaterialService.GetSigningKey();

        if (!await dbContext.Database.CanConnectAsync())
            throw new InvalidOperationException("Database not reachable.");
    }

    /// <summary>
    /// Readiness: lightweight DB connectivity check.
    /// Failure removes the pod from the Service (no traffic) but does NOT restart it.
    /// </summary>
    public async Task ReadinessProbe()
    {
        if (!await dbContext.Database.CanConnectAsync())
            throw new InvalidOperationException("Database not reachable.");
    }

    /// <summary>
    /// Liveness: keep cheap — no external dependencies.
    /// Only fails if the process itself is deadlocked/stuck.
    /// Failure restarts the pod, so never tie this to DB or downstream services.
    /// </summary>
    public Task LivenessProbe() => Task.CompletedTask;
}