namespace JOrder.Common.Attributes;

/// <summary>
/// Applies per-IP rate limiting to the decorated endpoint.
/// The limit is enforced by a fixed window (request count) and optionally
/// a concurrency cap (simultaneous in-flight requests).
/// </summary>
/// <remarks>
/// The limiter reads this attribute from endpoint metadata at request time,
/// so no named policy registration is required in each service.
/// Both limits are partitioned by the caller's remote IP address.
/// <para>
/// To express a per-second limit, set <paramref name="windowSeconds"/> to <c>1</c>.
/// </para>
/// </remarks>
/// <param name="permitLimit">
/// Maximum number of requests allowed within <paramref name="windowSeconds"/>.
/// </param>
/// <param name="windowSeconds">
/// Duration of the rate-limit window in seconds. Defaults to <c>60</c> (per minute).
/// </param>
/// <param name="maxConcurrentRequests">
/// Maximum number of requests allowed to execute simultaneously for this endpoint per IP.
/// Set to <c>0</c> (default) to disable the concurrency limit.
/// </param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RateLimitAttribute(
    int permitLimit,
    int windowSeconds = 60,
    int maxConcurrentRequests = 0) : Attribute
{
    /// <summary>Maximum number of requests allowed within <see cref="WindowSeconds"/>.</summary>
    public int PermitLimit { get; } = permitLimit;

    /// <summary>Duration of the rate-limit window in seconds.</summary>
    public int WindowSeconds { get; } = windowSeconds;

    /// <summary>
    /// Maximum simultaneous in-flight requests per IP. <c>0</c> means the concurrency
    /// limiter is disabled for this endpoint.
    /// </summary>
    public int MaxConcurrentRequests { get; } = maxConcurrentRequests;
}

