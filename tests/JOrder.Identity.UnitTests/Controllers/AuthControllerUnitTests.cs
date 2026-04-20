using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Application.Auth.Results;
using JOrder.Identity.Contracts.Requests;
using JOrder.Identity.Contracts.Responses;
using JOrder.Identity.Controllers;
using JOrder.Identity.Services.Interfaces;
using JOrder.Testing.Controllers;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Controllers;

public class AuthControllerUnitTests : ApiControllerUnitTestBase
{
    private readonly IAuthService _authService;
    private readonly AuthController _authController;
    
    public AuthControllerUnitTests()
    {
        _authService = Substitute.For<IAuthService>();
        _authController = new AuthController(_authService);
        
        AttachHttpContext(_authController, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");
    }

    [Fact]
    public async Task Register_Success_Returns_CreatedAtActionResult()
    {
        // Arrange
        _authService.RegisterAsync(Arg.Any<RegisterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokenResult>.Success(new AuthTokenResult("access_token",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "access_token",
                DateTimeOffset.UtcNow.AddDays(15))));

        var request = new RegisterRequestDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "test@example.com",
            Password = "Password1!",
        };
        
        // Act
        var result = await _authController.Register(request);

        // Assert
        var createdAt = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(UsersController.GetUserProfile), createdAt.ActionName);
        Assert.Equal("Users", createdAt.ControllerName);

        await _authService.Received(1).RegisterAsync(
            Arg.Is<RegisterCommand>(c => c.IpAddress == "127.0.0.1" && c.UserAgent == "JOrder.UnitTests/1.0"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_Failure_Returns_BadRequest()
    {
        // Arrange
        _authService.RegisterAsync(Arg.Any<RegisterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation("fail", "fail"));
        
        var request = new RegisterRequestDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "test@example.com",
            Password = "Password1!",
        };
        
        // Act
        var result = await _authController.Register(request);
        
        // Assert
        var badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task LogoutAll_Success_WithAuthenticatedUser_ReturnsNoContent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        AttachAuthenticatedHttpContext(_authController, userId, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");

        _authService.LogoutAllAsync(Arg.Any<LogoutAllCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _authController.LogoutAll();

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _authService.Received(1).LogoutAllAsync(
            Arg.Is<LogoutAllCommand>(c => c.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_Success_ReturnsOkWithTokens()
    {
        // Arrange
        _authService.LoginAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokenResult>.Success(new AuthTokenResult("access_token",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "refresh_token",
                DateTimeOffset.UtcNow.AddDays(7))));

        var request = new LoginRequestDto
        {
            Email = "john@example.com",
            Password = "Password1!"
        };

        // Act
        var result = await _authController.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var responseDto = Assert.IsType<LoginResponseDto>(okResult.Value);
        Assert.Equal("access_token", responseDto.AccessToken);

        await _authService.Received(1).LoginAsync(
            Arg.Is<LoginCommand>(c => c.Email == "john@example.com" && c.IpAddress == "127.0.0.1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_Failure_ReturnsBadRequest()
    {
        // Arrange
        _authService.LoginAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation("invalid_credentials", "Email or password is incorrect"));

        var request = new LoginRequestDto
        {
            Email = "john@example.com",
            Password = "WrongPassword"
        };

        // Act
        var result = await _authController.Login(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Refresh_Success_ReturnsOkWithTokens()
    {
        // Arrange
        _authService.RefreshAsync(Arg.Any<RefreshCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokenResult>.Success(new AuthTokenResult("new_access_token",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "new_refresh_token",
                DateTimeOffset.UtcNow.AddDays(7))));

        var request = new RefreshRequestDto { RefreshToken = "old_refresh_token" };

        // Act
        var result = await _authController.Refresh(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var responseDto = Assert.IsType<RefreshResponseDto>(okResult.Value);
        Assert.Equal("new_access_token", responseDto.AccessToken);

        await _authService.Received(1).RefreshAsync(
            Arg.Is<RefreshCommand>(c => c.RefreshToken == "old_refresh_token" && c.IpAddress == "127.0.0.1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_Failure_ReturnsUnauthorized()
    {
        // Arrange
        _authService.RefreshAsync(Arg.Any<RefreshCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.Unauthorized("invalid_token", "Refresh token is invalid or expired"));

        var request = new RefreshRequestDto { RefreshToken = "invalid_refresh_token" };

        // Act
        var result = await _authController.Refresh(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Logout_Success_ReturnsNoContent()
    {
        // Arrange
        _authService.LogoutAsync(Arg.Any<LogoutCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var request = new LogoutRequestDto { RefreshToken = "refresh_token_to_revoke" };

        // Act
        var result = await _authController.Logout(request);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _authService.Received(1).LogoutAsync(
            Arg.Is<LogoutCommand>(c => c.RefreshToken == "refresh_token_to_revoke"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_Failure_ReturnsBadRequest()
    {
        // Arrange
        _authService.LogoutAsync(Arg.Any<LogoutCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation("invalid_token", "Refresh token is invalid"));

        var request = new LogoutRequestDto { RefreshToken = "invalid_token" };

        // Act
        var result = await _authController.Logout(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task LogoutAll_InvalidUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        AttachHttpContext(_authController, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");
        // No authenticated user attached, so GetUserIdClaim returns null

        // Act
        var result = await _authController.LogoutAll();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        await _authService.DidNotReceive().LogoutAllAsync(Arg.Any<LogoutAllCommand>(), Arg.Any<CancellationToken>());
    }
}