using JOrder.Common.Abstractions.Results;
using JOrder.Common.Attributes;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Application.Auth.Results;
using JOrder.Identity.Application.Users.Commands;
using JOrder.Identity.Application.Users.Results;
using JOrder.Identity.Extensions;
using JOrder.Identity.Models;
using JOrder.Identity.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace JOrder.Identity.Services;

[ScopedService]
public sealed class UsersService(UserManager<User> userManager, ILogger<UsersService> logger): IUsersService
{
    public async Task<Result<UserProfileResult>> GetUserProfileAsync(UserProfileCommand command,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting user profile for user {UserId}", command.UserId);
        
        var user = await userManager.FindByIdAsync(command.UserId.ToString());
        
        if (user is null)        
        {
            logger.LogWarning("User with id {UserId} not found", command.UserId); 
            return Error.NotFound("users.not_found", "User not found.");
        }
        
        var roles = await userManager.GetRolesAsync(user);

        if (roles.Count == 0)
        {
            logger.LogWarning("User with id {UserId} no roles found", command.UserId);
            return Error.NotFound("users.roles.not_found", "User roles not found.");
        }
        
        var result = new UserProfileResult(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? "Unknown",
            [.. roles],
            user.IsActive
        );
            
        return result;
    }

    public async Task<Result> ChangePasswordAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation("Changing password for user {UserId}", command.UserId);
        
        var user = await userManager.FindByIdAsync(command.UserId.ToString());
        
        if (user is null)
        {
            logger.LogWarning("User with id {UserId} not found", command.UserId);
            return Error.NotFound("users.not_found", "User not found.");
        }
        
        var changedResult = await userManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);

        if (!changedResult.Succeeded)
        {
            logger.LogWarning("Failed to change password for user {UserId}. Errors: {Errors}", command.UserId, changedResult.Errors);
            return changedResult.ToValidationError("users.change_password_failed", "Failed to change password.");
        }

        return Result.Success();
    }

    public async Task<Result<UserProfileResult>> UpdateProfileAsync(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating profile for user {UserId}", command.UserId);

        var user = await userManager.FindByIdAsync(command.UserId.ToString());

        if (user is null)
        {
            logger.LogWarning("User with id {UserId} not found", command.UserId);
            return Error.NotFound("users.not_found", "User not found.");
        }

        if (command.FirstName is not null) user.FirstName = command.FirstName;
        if (command.LastName is not null) user.LastName = command.LastName;

        if (command.Email is not null && !string.Equals(user.Email, command.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailToken = await userManager.GenerateChangeEmailTokenAsync(user, command.Email);
            var emailResult = await userManager.ChangeEmailAsync(user, command.Email, emailToken);
            if (!emailResult.Succeeded)
            {
                logger.LogWarning("Failed to change email for user {UserId}. Errors: {Errors}", command.UserId, emailResult.Errors);
                return emailResult.ToValidationError("users.update_profile.email_failed", "Failed to update email.");
            }

            await userManager.SetUserNameAsync(user, command.Email);
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            logger.LogWarning("Failed to update profile for user {UserId}. Errors: {Errors}", command.UserId, updateResult.Errors);
            return updateResult.ToValidationError("users.update_profile_failed", "Failed to update profile.");
        }

        var roles = await userManager.GetRolesAsync(user);

        logger.LogInformation("Profile updated for user {UserId}", command.UserId);
        return new UserProfileResult(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? "Unknown",
            [.. roles],
            user.IsActive);
    }
}