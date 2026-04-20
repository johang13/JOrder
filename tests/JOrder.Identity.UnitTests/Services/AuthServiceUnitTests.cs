using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Models;
using JOrder.Identity.Persistence;
using JOrder.Identity.Services;
using JOrder.Identity.Services.Interfaces;
using JOrder.Identity.UnitTests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Services;

public class AuthServiceUnitTests : IDisposable
{
    private readonly UserManager<User> _userManager;
    private readonly ITokenMintingService _tokenMintingService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly JOrderIdentityDbContext _dbContext;
    private readonly SqliteConnection _connection;
    private readonly AuthService _authService;
    private readonly DateTimeOffset _now = new(2026, 4, 19, 12, 0, 0, TimeSpan.Zero);

    public AuthServiceUnitTests()
    {
        _userManager = IdentityTestHelpers.CreateUserManager();
        _tokenMintingService = Substitute.For<ITokenMintingService>();
        _refreshTokenService = Substitute.For<IRefreshTokenService>();
        (_dbContext, _connection) = IdentityTestHelpers.CreateSqliteContext();

        var logger = Substitute.For<ILogger<AuthService>>();
        _authService = new AuthService(
            _userManager,
            _tokenMintingService,
            _refreshTokenService,
            _dbContext,
            new JOrder.Testing.Time.FixedTimeProvider(_now),
            logger);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RegisterAsync_WhenUserAlreadyExists_ReturnsConflict()
    {
        // Arrange
        var command = new RegisterCommand("John", "Doe", "john@example.com", "Password1!", "127.0.0.1", "UnitTest");
        _userManager.FindByEmailAsync(command.Email).Returns(new User { Email = command.Email });

        // Act
        var result = await _authService.RegisterAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("auth.user_exists", result.Error.Code);
    }

    [Fact]
    public async Task RegisterAsync_WhenValidRequest_ReturnsTokensAndSavesRefreshToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "john@example.com",
            UserName = "john@example.com",
            IsActive = true
        };

        var command = new RegisterCommand(
            "John",
            "Doe",
            "john@example.com",
            "Password1!",
            "127.0.0.1",
            "UnitTest");

        _userManager.FindByEmailAsync(command.Email).Returns((User?)null);
        _userManager.CreateAsync(Arg.Any<User>(), command.Password).Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), "Customer").Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(Arg.Any<User>()).Returns(["Customer"]);

        _tokenMintingService.MintAccessToken(Arg.Any<User>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(("access-token", _now.AddMinutes(15)));
        _tokenMintingService.MintRefreshToken()
            .Returns(("refresh-raw", "refresh-hash", _now.AddDays(7)));

        // Act
        var result = await _authService.RegisterAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Equal("refresh-raw", result.Value.RefreshToken);

        await _refreshTokenService.Received(1).SaveAsync(
            Arg.Any<Guid>(),
            "refresh-hash",
            _now.AddDays(7),
            "127.0.0.1",
            "UnitTest",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var command = new LoginCommand("john@example.com", "wrong-password", "127.0.0.1", "UnitTest");
        _userManager.FindByEmailAsync(command.Email).Returns((User?)null);

        // Act
        var result = await _authService.LoginAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("auth.invalid_credentials", result.Error.Code);
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsValid_ReturnsTokensAndSavesRefreshToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "john@example.com",
            UserName = "john@example.com",
            IsActive = true
        };

        var command = new LoginCommand("john@example.com", "Password1!", "127.0.0.1", "UnitTest");

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(true);
        _userManager.GetRolesAsync(user).Returns(["Customer"]);

        _tokenMintingService.MintAccessToken(user, Arg.Any<IReadOnlyCollection<string>>())
            .Returns(("access-token", _now.AddMinutes(15)));
        _tokenMintingService.MintRefreshToken()
            .Returns(("refresh-raw", "refresh-hash", _now.AddDays(7)));

        // Act
        var result = await _authService.LoginAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Equal("refresh-raw", result.Value.RefreshToken);

        await _refreshTokenService.Received(1).SaveAsync(
            user.Id,
            "refresh-hash",
            _now.AddDays(7),
            "127.0.0.1",
            "UnitTest",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_WhenTokenNotFound_ReturnsUnauthorized()
    {
        // Arrange
        var command = new RefreshCommand("raw-token", "127.0.0.1", "UnitTest");
        _refreshTokenService.FindByRawTokenAsync(command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        // Act
        var result = await _authService.RefreshAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("auth.refresh.invalid", result.Error.Code);
    }

    [Fact]
    public async Task RefreshAsync_WhenTokenRevoked_ReturnsUnauthorized()
    {
        // Arrange
        var command = new RefreshCommand("raw-token", "127.0.0.1", "UnitTest");
        _refreshTokenService.FindByRawTokenAsync(command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(new RefreshToken
            {
                UserId = Guid.NewGuid(),
                TokenHash = "hash",
                ExpiresAt = _now.AddHours(1),
                IsRevoked = true,
                CreatedByIp = "127.0.0.1",
                UserAgent = "UnitTest"
            });

        // Act
        var result = await _authService.RefreshAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("auth.refresh.revoked", result.Error.Code);
    }

    [Fact]
    public async Task RefreshAsync_WhenTokenExpired_ReturnsUnauthorized()
    {
        // Arrange
        var command = new RefreshCommand("raw-token", "127.0.0.1", "UnitTest");
        _refreshTokenService.FindByRawTokenAsync(command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(new RefreshToken
            {
                UserId = Guid.NewGuid(),
                TokenHash = "hash",
                ExpiresAt = _now.AddSeconds(-1),
                CreatedByIp = "127.0.0.1",
                UserAgent = "UnitTest"
            });

        // Act
        var result = await _authService.RefreshAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("auth.refresh.expired", result.Error.Code);
    }

    [Fact]
    public async Task RefreshAsync_WhenUserInactive_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RefreshCommand("raw-token", "127.0.0.1", "UnitTest");

        _refreshTokenService.FindByRawTokenAsync(command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(new RefreshToken
            {
                UserId = userId,
                TokenHash = "hash",
                ExpiresAt = _now.AddHours(1),
                CreatedByIp = "127.0.0.1",
                UserAgent = "UnitTest"
            });

        _userManager.FindByIdAsync(userId.ToString()).Returns(new User
        {
            Id = userId,
            Email = "john@example.com",
            IsActive = false
        });

        // Act
        var result = await _authService.RefreshAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("auth.refresh.invalid", result.Error.Code);
    }

    [Fact]
    public async Task RefreshAsync_Success_RotatesTokenAndReturnsNewTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "john@example.com", UserName = "john@example.com", IsActive = true };
        var storedToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = "old-hash",
            ExpiresAt = _now.AddHours(1),
            CreatedByIp = "127.0.0.1",
            UserAgent = "UnitTest"
        };

        var command = new RefreshCommand("raw-token", "127.0.0.1", "UnitTest");

        _refreshTokenService.FindByRawTokenAsync(command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(storedToken);

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(["Customer"]);

        _tokenMintingService.MintRefreshToken().Returns(("new-raw-refresh", "new-hash", _now.AddDays(7)));
        _tokenMintingService.MintAccessToken(user, Arg.Any<IReadOnlyCollection<string>>())
            .Returns(("new-access-token", _now.AddMinutes(15)));

        // Act
        var result = await _authService.RefreshAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("new-access-token", result.Value.AccessToken);
        Assert.Equal("new-raw-refresh", result.Value.RefreshToken);

        await _refreshTokenService.Received(1).RotateAsync(
            storedToken,
            "new-hash",
            _now.AddDays(7),
            "127.0.0.1",
            "UnitTest",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_WhenTokenMissing_StillReturnsSuccess()
    {
        // Arrange
        var command = new LogoutCommand("missing-token");
        _refreshTokenService.FindByRawTokenAsync(command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        // Act
        var result = await _authService.LogoutAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await _refreshTokenService.DidNotReceive().RevokeAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_WhenTokenExists_RevokesTokenAndReturnsSuccess()
    {
        // Arrange
        var token = new RefreshToken
        {
            UserId = Guid.NewGuid(),
            TokenHash = "hash",
            ExpiresAt = _now.AddHours(1),
            CreatedByIp = "127.0.0.1",
            UserAgent = "UnitTest"
        };

        var command = new LogoutCommand("existing-token");
        _refreshTokenService.FindByRawTokenAsync(command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(token);

        // Act
        var result = await _authService.LogoutAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await _refreshTokenService.Received(1).RevokeAsync(token, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAllAsync_WhenUserMissing_ReturnsUnauthorized()
    {
        // Arrange
        var command = new LogoutAllCommand(Guid.NewGuid());
        _userManager.FindByIdAsync(command.UserId.ToString()).Returns((User?)null);

        // Act
        var result = await _authService.LogoutAllAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("auth.logout_all.invalid_user", result.Error.Code);
    }

    [Fact]
    public async Task LogoutAllAsync_WhenUserExists_RevokesAllTokens()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "john@example.com" };
        var command = new LogoutAllCommand(user.Id);

        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        // Act
        var result = await _authService.LogoutAllAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await _refreshTokenService.Received(1).RevokeAllAsync(user, Arg.Any<CancellationToken>());
    }
}


