using JOrder.Common.Abstractions.Results;
using JOrder.Common.Attributes;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Application.Auth.Results;
using JOrder.Identity.Models;
using JOrder.Identity.Persistence;
using JOrder.Identity.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace JOrder.Identity.Services;

[ScopedService]
public sealed class OAuth2Service(
    UserManager<User> userManager,
    ITokenMintingService tokenMintingService,
    IRefreshTokenService refreshTokenService,
    JOrderIdentityDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<OAuth2Service> logger) : IOAuth2Service
{
    public async Task<Result<AuthTokenResult>> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Logging in user: {Email}", command.Email);

        var user = await userManager.FindByEmailAsync(command.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, command.Password))
        {
            logger.LogInformation("Invalid credentials for: {Email}", command.Email);
            return Error.Unauthorized("auth.invalid_credentials", "Invalid email or password.");
        }

        var roles = await GetRolesOrError(user);
        if (roles is null)
            return Error.Unauthorized("auth.login.no_roles", "User has no assigned roles.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var tokens = await IssueTokensAsync(user, roles, command.IpAddress, command.UserAgent, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AuthTokenResult(tokens.AccessToken, tokens.AccessTokenExpiresAt, tokens.RefreshToken,
                tokens.RefreshTokenExpiresAt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login failed for {Email}, rolling back transaction", command.Email);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Result<AuthTokenResult>> RefreshAsync(RefreshCommand command,
        CancellationToken cancellationToken = default)
    {
        var storedToken = await refreshTokenService.FindByRawTokenAsync(command.RefreshToken, cancellationToken);

        if (storedToken is null)
            return Error.Unauthorized("auth.refresh.invalid", "Invalid refresh token.");

        if (storedToken.IsRevoked)
        {
            logger.LogWarning("Revoked refresh token used for user {UserId}", storedToken.UserId);
            return Error.Unauthorized("auth.refresh.revoked", "Refresh token has been revoked.");
        }

        if (storedToken.ExpiresAt < timeProvider.GetUtcNow())
        {
            logger.LogWarning("Expired refresh token used for user {UserId}", storedToken.UserId);
            return Error.Unauthorized("auth.refresh.expired", "Refresh token has expired.");
        }

        var user = await userManager.FindByIdAsync(storedToken.UserId.ToString());
        if (user is null || !user.IsActive)
            return Error.Unauthorized("auth.refresh.invalid", "Invalid refresh token.");

        var roles = await GetRolesOrError(user);
        if (roles is null)
            return Error.Unauthorized("auth.refresh.no_roles", "User has no assigned roles.");

        var (rawRefreshToken, newTokenHash, refreshTokenExpiresAt) = tokenMintingService.MintRefreshToken();
        var (accessToken, accessTokenExpiresAt) = tokenMintingService.MintAccessToken(user, roles);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await refreshTokenService.RotateAsync(storedToken, newTokenHash, refreshTokenExpiresAt, command.IpAddress,
                command.UserAgent, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Token refresh failed for {UserId}, rolling back transaction", user.Id);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        logger.LogInformation("Token refreshed for user {UserId}", user.Id);
        return new AuthTokenResult(accessToken, accessTokenExpiresAt, rawRefreshToken, refreshTokenExpiresAt);
    }

    public async Task<Result> RevokeAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        var token = await refreshTokenService.FindByRawTokenAsync(command.RefreshToken, cancellationToken);
        if (token is not null)
            await refreshTokenService.RevokeAsync(token, cancellationToken);

        return Result.Success();
    }

    private async Task<(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt)>
        IssueTokensAsync(User user, IReadOnlyCollection<string> roles, string ipAddress, string userAgent,
            CancellationToken cancellationToken)
    {
        var (accessToken, accessTokenExpiresAt) = tokenMintingService.MintAccessToken(user, roles);
        var (rawRefreshToken, tokenHash, refreshTokenExpiresAt) = tokenMintingService.MintRefreshToken();

        await refreshTokenService.SaveAsync(user.Id, tokenHash, refreshTokenExpiresAt, ipAddress, userAgent, cancellationToken);

        return (accessToken, accessTokenExpiresAt, rawRefreshToken, refreshTokenExpiresAt);
    }

    private async Task<IReadOnlyCollection<string>?> GetRolesOrError(User user)
    {
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count == 0)
        {
            logger.LogError("No roles found for user {UserId}", user.Id);
            return null;
        }

        return roles.ToArray();
    }
}
