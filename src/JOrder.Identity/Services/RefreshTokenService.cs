using JOrder.Common.Attributes;
using JOrder.Identity.Models;
using JOrder.Identity.Persistence;
using JOrder.Identity.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JOrder.Identity.Services;

[ScopedService]
public sealed class RefreshTokenService(
    JOrderIdentityDbContext dbContext,
    ITokenMintingService tokenMintingService,
    TimeProvider timeProvider) : IRefreshTokenService
{
    public async Task<RefreshToken?> FindByRawTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenMintingService.HashToken(rawToken);
        return await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);
    }

    public async Task SaveAsync(
        Guid userId, string tokenHash, DateTimeOffset expiresAt,
        string ipAddress, string userAgent,
        CancellationToken cancellationToken = default)
    {
        dbContext.RefreshTokens.Add(BuildToken(userId, tokenHash, expiresAt, ipAddress, userAgent));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshToken> RotateAsync(
        RefreshToken oldToken, string newTokenHash, DateTimeOffset expiresAt,
        string ipAddress, string userAgent,
        CancellationToken cancellationToken = default)
    {
        var newToken = BuildToken(oldToken.UserId, newTokenHash, expiresAt, ipAddress, userAgent);

        RevokeAndRotateToken(oldToken, newToken);

        dbContext.RefreshTokens.Add(newToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return newToken;
    }

    public async Task RevokeAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        RevokeAndRotateToken(token, null);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(User user, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        await dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .Where(rt => !rt.IsRevoked && rt.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.ReplacedByTokenId, (Guid?)null)
                    .SetProperty(rt => rt.ReplacedAt, now),
                cancellationToken);
    }

    
    private void RevokeAndRotateToken(RefreshToken oldToken, RefreshToken? newToken)
    {
        oldToken.IsRevoked = true;
        oldToken.ReplacedByTokenId = newToken?.Id ?? null;
        oldToken.ReplacedAt = timeProvider.GetUtcNow();
    }
    
    private static RefreshToken BuildToken(
        Guid userId, string tokenHash, DateTimeOffset expiresAt,
        string ipAddress, string userAgent) =>
        new()
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedByIp = ipAddress,
            UserAgent = userAgent,
        };
}
