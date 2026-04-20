using JOrder.Common.Models.Interfaces;
using JOrder.Common.Options;
using JOrder.Common.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JOrder.Common.Persistence;

public class AuditableInterceptor : SaveChangesInterceptor
{
    private readonly ServiceOptions _serviceOptions;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuditableInterceptor> _logger;
    
    public AuditableInterceptor(IOptions<ServiceOptions> serviceOptions, ICurrentUser currentUser, TimeProvider timeProvider, ILogger<AuditableInterceptor> logger)
    {
        _serviceOptions = serviceOptions.Value ?? throw new ArgumentNullException(nameof(serviceOptions));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditableEntities(DbContext? context)
    {
        if (context == null) return;

        var actor = GetAuditActor();
        var now = _timeProvider.GetUtcNow();
        var entries = context.ChangeTracker.Entries<IAuditable>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedById = _currentUser.Id;
                entry.Entity.CreatedBy = actor;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                entry.Property(nameof(IAuditable.CreatedById)).IsModified = false;

                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedById = _currentUser.Id;
                entry.Entity.UpdatedBy = actor;
            }
        }
    }
    
    private string GetAuditActor()
    {
        if (!_currentUser.IsAuthenticated)
            return _serviceOptions.Name;

        return _currentUser.Email ?? _serviceOptions.Name;
    }
}