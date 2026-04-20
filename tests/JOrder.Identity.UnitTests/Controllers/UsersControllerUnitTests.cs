using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Application.Users.Commands;
using JOrder.Identity.Application.Users.Results;
using JOrder.Identity.Contracts.Requests;
using JOrder.Identity.Contracts.Responses;
using JOrder.Identity.Controllers;
using JOrder.Identity.Services.Interfaces;
using JOrder.Testing.Controllers;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Controllers;

public class UsersControllerUnitTests : ApiControllerUnitTestBase
{
    private readonly IUsersService _usersService;
    private readonly UsersController _usersController;

    public UsersControllerUnitTests()
    {
        _usersService = Substitute.For<IUsersService>();
        _usersController = new UsersController(_usersService);

        AttachHttpContext(_usersController, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");
    }

    [Fact]
    public async Task GetUserProfile_Success_ReturnsOkWithProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        AttachAuthenticatedHttpContext(_usersController, userId, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");

        _usersService.GetUserProfileAsync(Arg.Any<UserProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserProfileResult>.Success(new UserProfileResult(
                userId,
                "John",
                "Doe",
                "john@example.com",
                ["User", "Admin"],
                true)));

        // Act
        var result = await _usersController.GetUserProfile();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal(userId, profile.Id);
        Assert.Equal("John", profile.FirstName);
        Assert.Equal("Doe", profile.LastName);
        Assert.Equal("john@example.com", profile.Email);
        Assert.Equal(["User", "Admin"], profile.Roles);

        await _usersService.Received(1).GetUserProfileAsync(
            Arg.Is<UserProfileCommand>(c => c.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUserProfile_InvalidUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        AttachHttpContext(_usersController, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");

        // Act
        var result = await _usersController.GetUserProfile();

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
        await _usersService.DidNotReceive().GetUserProfileAsync(Arg.Any<UserProfileCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUserProfile_Failure_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        AttachAuthenticatedHttpContext(_usersController, userId, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");

        _usersService.GetUserProfileAsync(Arg.Any<UserProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("user_not_found", "User not found"));

        // Act
        var result = await _usersController.GetUserProfile();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateProfile_Success_ReturnsOkWithUpdatedProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        AttachAuthenticatedHttpContext(_usersController, userId, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");

        var request = new UpdateProfileRequestDto
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com"
        };

        _usersService.UpdateProfileAsync(Arg.Any<UpdateProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserProfileResult>.Success(new UserProfileResult(
                userId,
                "Jane",
                "Smith",
                "jane.smith@example.com",
                ["User"],
                true)));

        // Act
        var result = await _usersController.UpdateProfile(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal(userId, profile.Id);
        Assert.Equal("Jane", profile.FirstName);
        Assert.Equal("Smith", profile.LastName);
        Assert.Equal("jane.smith@example.com", profile.Email);
        Assert.Equal(["User"], profile.Roles);

        await _usersService.Received(1).UpdateProfileAsync(
            Arg.Is<UpdateProfileCommand>(c =>
                c.UserId == userId &&
                c.FirstName == "Jane" &&
                c.LastName == "Smith" &&
                c.Email == "jane.smith@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProfile_InvalidUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        var request = new UpdateProfileRequestDto { FirstName = "NewName" };

        // Act
        var result = await _usersController.UpdateProfile(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
        await _usersService.DidNotReceive().UpdateProfileAsync(Arg.Any<UpdateProfileCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProfile_Failure_ReturnsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        AttachAuthenticatedHttpContext(_usersController, userId, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");

        var request = new UpdateProfileRequestDto { Email = "invalid-email" };

        _usersService.UpdateProfileAsync(Arg.Any<UpdateProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation("invalid_email", "Email is invalid"));

        // Act
        var result = await _usersController.UpdateProfile(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ChangePassword_Success_ReturnsNoContent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        AttachAuthenticatedHttpContext(_usersController, userId, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");

        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "CurrentPassword1!",
            NewPassword = "NewPassword1!"
        };

        _usersService.ChangePasswordAsync(Arg.Any<ChangePasswordCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _usersController.ChangePassword(request);

        // Assert
        Assert.IsType<NoContentResult>(result);

        await _usersService.Received(1).ChangePasswordAsync(
            Arg.Is<ChangePasswordCommand>(c =>
                c.UserId == userId &&
                c.CurrentPassword == "CurrentPassword1!" &&
                c.NewPassword == "NewPassword1!"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePassword_InvalidUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "CurrentPassword1!",
            NewPassword = "NewPassword1!"
        };

        // Act
        var result = await _usersController.ChangePassword(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        await _usersService.DidNotReceive().ChangePasswordAsync(Arg.Any<ChangePasswordCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePassword_Failure_ReturnsUnauthorizedObjectResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        AttachAuthenticatedHttpContext(_usersController, userId, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");

        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "WrongPassword1!",
            NewPassword = "NewPassword1!"
        };

        _usersService.ChangePasswordAsync(Arg.Any<ChangePasswordCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.Unauthorized("invalid_password", "Current password is incorrect"));

        // Act
        var result = await _usersController.ChangePassword(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}

