using JOrder.Common.Abstractions.Results;
using JOrder.Common.Attributes;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Application.Users.Commands;
using JOrder.Identity.Application.Users.Results;
using JOrder.Identity.Extensions;
using JOrder.Identity.Models;
using JOrder.Identity.Persistence;
using JOrder.Identity.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace JOrder.Identity.Services;

[ScopedService]
public sealed class UsersService(
    UserManager<User> userManager,
    JOrderIdentityDbContext dbContext,
    ILogger<UsersService> logger) : IUsersService
{
    public async Task<Result> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Registering user: {Email}", command.Email);

        if (await userManager.FindByEmailAsync(command.Email) is not null)
        {
            logger.LogInformation("User with email {Email} already exists", command.Email);
            return Error.Conflict("auth.user_exists", "A user with this email already exists.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = new User
            {
                FirstName = command.FirstName,
                LastName = command.LastName,
                Email = command.Email,
                UserName = command.Email,
            };

            var createResult = await userManager.CreateAsync(user, command.Password);
            if (!createResult.Succeeded)
            {
                logger.LogInformation("User creation failed for user: {Email}", command.Email);
                return createResult.ToValidationError("auth.register.invalid", "User creation failed.");
            }

            logger.LogInformation("User created: {Email}", command.Email);

            var roleResult = await userManager.AddToRoleAsync(user, "Customer");
            if (!roleResult.Succeeded)
            {
                logger.LogInformation("Adding role to user failed for user: {Email}", command.Email);
                return roleResult.ToValidationError("auth.register.role_assignment_failed", "Failed to assign role to user.");
            }

            logger.LogInformation("Assigned role 'Customer' to user: {Email}", command.Email);

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Registration failed for {Email}, rolling back transaction", command.Email);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

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