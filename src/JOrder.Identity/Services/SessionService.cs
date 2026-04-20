using JOrder.Common.Abstractions.Results;
using JOrder.Common.Attributes;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Models;
using JOrder.Identity.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace JOrder.Identity.Services;

[ScopedService]
public sealed class SessionService(
    UserManager<User> userManager,
    IRefreshTokenService refreshTokenService,
    ILogger<SessionService> logger) : ISessionService
{
    public async Task<Result> LogoutAllAsync(LogoutAllCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString());
        if (user is null)
        {
            logger.LogError("No user found for user {UserId}", command.UserId);
            return Error.Unauthorized("auth.logout_all.invalid_user", "Invalid user.");
        }

        await refreshTokenService.RevokeAllAsync(user, cancellationToken);

        return Result.Success();
    }
}
