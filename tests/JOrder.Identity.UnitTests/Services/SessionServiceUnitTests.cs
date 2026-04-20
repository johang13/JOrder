using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Models;
using JOrder.Identity.Services;
using JOrder.Identity.Services.Interfaces;
using JOrder.Identity.UnitTests.TestInfrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Services;

public class SessionServiceUnitTests
{
    private readonly UserManager<User> _userManager;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly SessionService _sessionService;

    public SessionServiceUnitTests()
    {
        _userManager = IdentityTestHelpers.CreateUserManager();
        _refreshTokenService = Substitute.For<IRefreshTokenService>();
        var logger = Substitute.For<ILogger<SessionService>>();

        _sessionService = new SessionService(_userManager, _refreshTokenService, logger);
    }

    [Fact]
    public async Task LogoutAllAsync_WhenUserMissing_ReturnsUnauthorized()
    {
        var command = new LogoutAllCommand(Guid.NewGuid());
        _userManager.FindByIdAsync(command.UserId.ToString()).Returns((User?)null);

        var result = await _sessionService.LogoutAllAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("auth.logout_all.invalid_user", result.Error.Code);
    }

    [Fact]
    public async Task LogoutAllAsync_WhenUserExists_RevokesAllTokens()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "john@example.com" };
        var command = new LogoutAllCommand(user.Id);

        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        var result = await _sessionService.LogoutAllAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _refreshTokenService.Received(1).RevokeAllAsync(user, Arg.Any<CancellationToken>());
    }
}
