using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Application.Users.Commands;
using JOrder.Identity.IntegrationTests.TestInfrastructure;
using JOrder.Identity.Models;
using JOrder.Identity.Persistence;
using JOrder.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace JOrder.Identity.IntegrationTests.Services;

[Collection(IdentityPostgresIntegrationCollection.Name)]
public sealed class UsersServiceIntegrationTests(PostgresIntegrationFixture fixture)
{
    [Fact]
    public async Task GetUserProfileAsync_UserMissing_ReturnsNotFound()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);
        var (service, _) = CreateUsersService(context);

        var result = await service.GetUserProfileAsync(new UserProfileCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("users.not_found", result.Error.Code);
    }

    [Fact]
    public async Task GetUserProfileAsync_UserWithoutRoles_ReturnsRolesNotFound()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);
        var (service, userManager) = CreateUsersService(context);

        var user = PostgresIntegrationFixture.CreateUser("noroles@example.com");
        var created = await userManager.CreateAsync(user, "Password1!");
        Assert.True(created.Succeeded);

        var result = await service.GetUserProfileAsync(new UserProfileCommand(user.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("users.roles.not_found", result.Error.Code);
    }

    [Fact]
    public async Task GetUserProfileAsync_Success_ReturnsProfileWithRoles()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);
        var (service, userManager) = CreateUsersService(context);

        var user = PostgresIntegrationFixture.CreateUser("profile@example.com");
        var created = await userManager.CreateAsync(user, "Password1!");
        Assert.True(created.Succeeded);

        var addedRole = await userManager.AddToRoleAsync(user, "Customer");
        Assert.True(addedRole.Succeeded);

        var result = await service.GetUserProfileAsync(new UserProfileCommand(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.Id);
        Assert.Equal("profile@example.com", result.Value.Email);
        Assert.Contains("Customer", result.Value.Roles);
    }

    [Fact]
    public async Task ChangePasswordAsync_UserMissing_ReturnsNotFound()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);
        var (service, _) = CreateUsersService(context);

        var result = await service.ChangePasswordAsync(
            new ChangePasswordCommand(Guid.NewGuid(), "Password1!", "Password2!"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("users.not_found", result.Error.Code);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsValidationError()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);
        var (service, userManager) = CreateUsersService(context);

        var user = PostgresIntegrationFixture.CreateUser("changepassword@example.com");
        var created = await userManager.CreateAsync(user, "Password1!");
        Assert.True(created.Succeeded);

        var result = await service.ChangePasswordAsync(
            new ChangePasswordCommand(user.Id, "WrongPassword1!", "Password2!"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("users.change_password_failed", result.Error.Code);
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_UpdatesStoredPassword()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);
        var (service, userManager) = CreateUsersService(context);

        var user = PostgresIntegrationFixture.CreateUser("changepassword.success@example.com");
        var created = await userManager.CreateAsync(user, "Password1!");
        Assert.True(created.Succeeded);

        var result = await service.ChangePasswordAsync(
            new ChangePasswordCommand(user.Id, "Password1!", "Password2!"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(await userManager.CheckPasswordAsync(user, "Password2!"));
    }

    [Fact]
    public async Task UpdateProfileAsync_Success_UpdatesFirstAndLastName()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);
        var (service, userManager) = CreateUsersService(context);

        var user = PostgresIntegrationFixture.CreateUser("updateprofile@example.com");
        var created = await userManager.CreateAsync(user, "Password1!");
        Assert.True(created.Succeeded);

        var addedRole = await userManager.AddToRoleAsync(user, "Customer");
        Assert.True(addedRole.Succeeded);

        var result = await service.UpdateProfileAsync(
            new UpdateProfileCommand(user.Id, "Updated", "Name", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated", result.Value.FirstName);
        Assert.Equal("Name", result.Value.LastName);

        var updated = await userManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated!.FirstName);
        Assert.Equal("Name", updated.LastName);
    }

    [Fact]
    public async Task UpdateProfileAsync_Success_UpdatesEmailAndUsername()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);
        var (service, userManager) = CreateUsersService(context);

        var user = PostgresIntegrationFixture.CreateUser("emailchange.old@example.com");
        var created = await userManager.CreateAsync(user, "Password1!");
        Assert.True(created.Succeeded);

        var result = await service.UpdateProfileAsync(
            new UpdateProfileCommand(user.Id, null, null, "emailchange.new@example.com"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("emailchange.new@example.com", result.Value.Email);

        var updated = await userManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(updated);
        Assert.Equal("emailchange.new@example.com", updated!.Email);
        Assert.Equal("emailchange.new@example.com", updated.UserName);
    }

    private static (UsersService Service, UserManager<User> UserManager) CreateUsersService(JOrderIdentityDbContext context)
    {
        var userStore = new UserStore<User, Role, JOrderIdentityDbContext, Guid>(context);
        var userManager = IdentityIntegrationTestHelpers.CreateUserManager(userStore);
        var service = new UsersService(userManager, Substitute.For<ILogger<UsersService>>());
        return (service, userManager);
    }
}

