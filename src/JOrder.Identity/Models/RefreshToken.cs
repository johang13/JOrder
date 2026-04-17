using JOrder.Common.Models;

namespace JOrder.Identity.Models;

public class RefreshToken : AuditableEntity
{
    public Guid UserId { get; init; }
    public string TokenHash { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public bool IsRevoked { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public DateTimeOffset? ReplacedAt { get; set; }
    public string CreatedByIp { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}