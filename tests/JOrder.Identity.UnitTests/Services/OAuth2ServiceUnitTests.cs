using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Models;
using JOrder.Identity.Persistence;
using JOrder.Identity.Services;
using JOrder.Identity.Services.Interfaces;
using JOrder.Identity.UnitTests.TestInfrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Services;

public class OAuth2ServiceUnitTests : IDisposable
{
    private readonly UserManager<User> _userManager;
    private readonly ITokenMintingService _tokenMintingService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly JOrderIdentityDbContext _dbContext;
    private readonly SqliteConnection _connection;
    private readonly OAuth2Service _oauth2Service;
    private readonly DateTimeOffset _now = new(2026, 4, 19, 12, 0, 0, TimeSpan.Zero);

    public OAuth2ServiceUnitTests()
    {
        _userManager = IdentityTestHelpers.CreateUserManager();
        _tokenMintingService = Substitute.For<ITokenMintingService>();
        _refreshTokenService = Substitute.For<IRefreshTokenService>();
        (_dbContext, _connection) = IdentityTestHelpers.CreateSqliteContext();

        var logger = Substitute.For<ILogger<OAuth2Service>>();
        _oauth2Service = new OAuth2Service(
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
    public async Task LoginAsync_InvalidCredentials_ReturnsUnauthorized()
    {
        var command = new LoginCommand("john@example.com", "wrong-password", "127.0.0.1", "UnitTest");
        _userManager.FindByEmailAsync(command.Email).Returns((User?)null);

        var result = await _oauth2Service.LoginAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("auth.invalid_credentials", result.Error.Code);
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsValid_ReturnsTokensAndSavesRefreshToken()
    {
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

        var result = await _oauth2Service.LoginAsync(command, CancellationToken.None);

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
        var command = new RefreshCommand("raw-token", "127.0.0.1", "UnitTest");
        _refreshTokenService.FindByRawTokenAsync(command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var result = await _oauth2Service.RefreshAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("auth.refresh.invalid", result.Error.Code);
    }

    [Fact]
    public async Task RefreshAsync_WhenTokenRevoked_ReturnsUnauthorized()
    {
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

        var result = await _oauth2Service.RefreshAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("auth.refresh.revoked", result.Error.Code);
    }

    [Fact]
    public async Task RefreshAsync_WhenTokenExpired_ReturnsUnauthorized()
    {
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

        var result = await _oauth2Service.RefreshAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("auth.refresh.expired", result.Error.Code);
    }

    [Fact]
    public async Task RefreshAsync_WhenUserInactive_ReturnsUnauthorized()
    {
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

        var result = await _oauth2Service.RefreshAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("auth.refresh.invalid", result.Error.Code);
    }

    [Fact]
    public async Task RefreshAsync_Success_RotatesTokenAndReturnsNewTokens()
    {
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

        var result = await _oauth2Service.RefreshAsync(command, CancellationToken.None);

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
    public async Task RevokeAsync_WhenTokenMissing_StillReturnsSuccess()
    {
        var command = new LogoutCommand("missing-token");
        _refreshTokenService.FindByRawTokenAsync(command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var result = await _oauth2Service.RevokeAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _refreshTokenService.DidNotReceive().RevokeAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_WhenTokenExists_RevokesToken()
    {
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

        var result = await _oauth2Service.RevokeAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _refreshTokenService.Received(1).RevokeAsync(token, Arg.Any<CancellationToken>());
    }
}
