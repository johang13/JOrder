using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Application.Users.Commands;
using JOrder.Identity.Models;
using JOrder.Identity.Services;
using JOrder.Identity.UnitTests.TestInfrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Services;

public class UsersServiceUnitTests
{
    private readonly UserManager<User> _userManager;
    private readonly UsersService _usersService;

    public UsersServiceUnitTests()
    {
        _userManager = IdentityTestHelpers.CreateUserManager();
        var logger = Substitute.For<ILogger<UsersService>>();
        _usersService = new UsersService(_userManager, logger);
    }

    [Fact]
    public async Task GetUserProfileAsync_UserMissing_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userManager.FindByIdAsync(userId.ToString()).Returns((User?)null);

        // Act
        var result = await _usersService.GetUserProfileAsync(new UserProfileCommand(userId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("users.not_found", result.Error.Code);
    }

    [Fact]
    public async Task GetUserProfileAsync_NoRoles_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, FirstName = "John", LastName = "Doe", Email = "john@example.com" };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns([]);

        // Act
        var result = await _usersService.GetUserProfileAsync(new UserProfileCommand(userId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("users.roles.not_found", result.Error.Code);
    }

    [Fact]
    public async Task GetUserProfileAsync_Success_ReturnsProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, FirstName = "John", LastName = "Doe", Email = "john@example.com", IsActive = true };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(["Customer", "Admin"]);

        // Act
        var result = await _usersService.GetUserProfileAsync(new UserProfileCommand(userId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.Id);
        Assert.Equal("john@example.com", result.Value.Email);
        Assert.Equal(["Customer", "Admin"], result.Value.Roles);
    }

    [Fact]
    public async Task ChangePasswordAsync_UserMissing_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userManager.FindByIdAsync(userId.ToString()).Returns((User?)null);

        // Act
        var result = await _usersService.ChangePasswordAsync(
            new ChangePasswordCommand(userId, "OldPass1!", "NewPass1!"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("users.not_found", result.Error.Code);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenIdentityFails_ReturnsValidationError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "john@example.com" };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        _userManager.ChangePasswordAsync(user, "OldPass1!", "NewPass1!")
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Current password is invalid." }));

        // Act
        var result = await _usersService.ChangePasswordAsync(
            new ChangePasswordCommand(userId, "OldPass1!", "NewPass1!"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("users.change_password_failed", result.Error.Code);
        Assert.Equal("Current password is invalid.", result.Error.Description);
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "john@example.com" };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.ChangePasswordAsync(user, "OldPass1!", "NewPass1!").Returns(IdentityResult.Success);

        // Act
        var result = await _usersService.ChangePasswordAsync(
            new ChangePasswordCommand(userId, "OldPass1!", "NewPass1!"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenEmailChangeFails_ReturnsValidationError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, FirstName = "John", LastName = "Doe", Email = "john@example.com", UserName = "john@example.com" };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GenerateChangeEmailTokenAsync(user, "new@example.com").Returns("token");
        _userManager.ChangeEmailAsync(user, "new@example.com", "token")
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Email already in use." }));

        // Act
        var result = await _usersService.UpdateProfileAsync(
            new UpdateProfileCommand(userId, "John", "Doe", "new@example.com"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("users.update_profile.email_failed", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task UpdateProfileAsync_Success_UpdatesNameAndEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, FirstName = "John", LastName = "Doe", Email = "john@example.com", UserName = "john@example.com", IsActive = true };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GenerateChangeEmailTokenAsync(user, "new@example.com").Returns("token");
        _userManager.ChangeEmailAsync(user, "new@example.com", "token").Returns(_ =>
        {
            user.Email = "new@example.com";
            return IdentityResult.Success;
        });
        _userManager.SetUserNameAsync(user, "new@example.com").Returns(IdentityResult.Success);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(user).Returns(["Customer"]);

        var command = new UpdateProfileCommand(userId, "Jane", "Smith", "new@example.com");

        // Act
        var result = await _usersService.UpdateProfileAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Jane", result.Value.FirstName);
        Assert.Equal("Smith", result.Value.LastName);
        Assert.Equal("new@example.com", result.Value.Email);

        await _userManager.Received(1).SetUserNameAsync(user, "new@example.com");
    }
}


