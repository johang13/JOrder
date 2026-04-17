using JOrder.Identity.Models;

namespace JOrder.Identity.Services.Interfaces;

public interface IRefreshTokenService
{
    Task<RefreshToken?> FindByRawTokenAsync(string rawToken, CancellationToken cancellationToken = default);

    Task SaveAsync(Guid userId, string tokenHash, DateTimeOffset expiresAt,
        string ipAddress, string userAgent, CancellationToken cancellationToken = default);

    Task<RefreshToken> RotateAsync(RefreshToken oldToken, string newTokenHash, DateTimeOffset expiresAt,
        string ipAddress, string userAgent, CancellationToken cancellationToken = default);

    Task RevokeAsync(RefreshToken token, CancellationToken cancellationToken = default);
    
    Task RevokeAllAsync(User user, CancellationToken cancellationToken = default);
}

