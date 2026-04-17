namespace JOrder.Common.Services.Interfaces;

/// <summary>
/// Represents a startup warmup task executed by
/// <see cref="JOrder.Common.Extensions.WebApplicationExtensions.RunWarmupTasksAsync"/>
/// before the application begins accepting traffic.
/// Register implementations via
/// <see cref="JOrder.Common.Extensions.HostApplicationExtensions.AddJOrderWarmupTask{TWarmupTask}"/>.
/// </summary>
public interface IJOrderWarmupTask
{
    /// <summary>
    /// Executes the warmup task.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ExecuteAsync(CancellationToken cancellationToken);
}