using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.IntegrationTests.TestInfrastructure;
using JOrder.Identity.Models;
using JOrder.Identity.Persistence;
using JOrder.Identity.Services;
using JOrder.Identity.Services.Interfaces;
using JOrder.Testing.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace JOrder.Identity.IntegrationTests.Services;

[Collection(IdentityPostgresIntegrationCollection.Name)]
public sealed class OAuth2ServiceIntegrationTests(PostgresIntegrationFixture fixture)
{
    [Fact]
    public async Task LoginAsync_Success_PersistsRefreshTokenAndReturnsTokens()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);

        var service = CreateOAuth2Service(context, TimeProvider.System, out var userManager, out var tokenMintingService);

        var user = PostgresIntegrationFixture.CreateUser("login.success@example.com");
        var created = await userManager.CreateAsync(user, "Password1!");
        Assert.True(created.Succeeded);

        var addedRole = await userManager.AddToRoleAsync(user, "Customer");
        Assert.True(addedRole.Succeeded);

        var now = DateTimeOffset.UtcNow;
        tokenMintingService.MintAccessToken(Arg.Any<User>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(("login-access-token", now.AddMinutes(15)));
        tokenMintingService.MintRefreshToken()
            .Returns(("login-raw-refresh", "login-refresh-hash", now.AddDays(7)));

        var command = new LoginCommand(
            "login.success@example.com",
            "Password1!",
            "127.0.0.1",
            "IntegrationTests");

        var result = await service.LoginAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("login-access-token", result.Value.AccessToken);
        Assert.Equal("login-raw-refresh", result.Value.RefreshToken);

        var savedToken = await context.RefreshTokens.SingleAsync(rt => rt.UserId == user.Id);
        Assert.Equal("login-refresh-hash", savedToken.TokenHash);
        Assert.Equal("127.0.0.1", savedToken.CreatedByIp);
        Assert.Equal("IntegrationTests", savedToken.UserAgent);
    }

    [Fact]
    public async Task RefreshAsync_Success_RotatesTokenAndReturnsNewTokens()
    {
        var now = DateTimeOffset.UtcNow;
        var fixedTimeProvider = new FixedTimeProvider(now);

        await using var context = await fixture.CreateContextAsync(fixedTimeProvider);

        var service = CreateOAuth2Service(context, fixedTimeProvider, out var userManager, out var tokenMintingService);

        var user = PostgresIntegrationFixture.CreateUser("refresh.success@example.com");
        var created = await userManager.CreateAsync(user, "Password1!");
        Assert.True(created.Succeeded);

        var addedRole = await userManager.AddToRoleAsync(user, "Customer");
        Assert.True(addedRole.Succeeded);

        var oldToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "old-refresh-hash",
            ExpiresAt = now.AddHours(2),
            CreatedByIp = "127.0.0.1",
            UserAgent = "IntegrationTests"
        };
        context.RefreshTokens.Add(oldToken);
        await context.SaveChangesAsync();

        tokenMintingService.HashToken("raw-old-refresh").Returns("old-refresh-hash");
        tokenMintingService.MintRefreshToken()
            .Returns(("raw-new-refresh", "new-refresh-hash", now.AddDays(7)));
        tokenMintingService.MintAccessToken(Arg.Any<User>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(("refresh-access-token", now.AddMinutes(15)));

        var result = await service.RefreshAsync(new RefreshCommand(
            "raw-old-refresh",
            "10.0.0.2",
            "Mobile"));

        Assert.True(result.IsSuccess);
        Assert.Equal("refresh-access-token", result.Value.AccessToken);
        Assert.Equal("raw-new-refresh", result.Value.RefreshToken);

        await context.Entry(oldToken).ReloadAsync();
        Assert.True(oldToken.IsRevoked);
        Assert.NotNull(oldToken.ReplacedAt);
        Assert.NotNull(oldToken.ReplacedByTokenId);

        var replacement = await context.RefreshTokens.SingleAsync(rt => rt.TokenHash == "new-refresh-hash");
        Assert.Equal(user.Id, replacement.UserId);
        Assert.Equal("10.0.0.2", replacement.CreatedByIp);
        Assert.Equal("Mobile", replacement.UserAgent);
        Assert.Equal(oldToken.ReplacedByTokenId, replacement.Id);
    }

    [Fact]
    public async Task RevokeAsync_Success_RevokesMatchingRefreshToken()
    {
        var now = DateTimeOffset.UtcNow;
        var fixedTimeProvider = new FixedTimeProvider(now);

        await using var context = await fixture.CreateContextAsync(fixedTimeProvider);

        var service = CreateOAuth2Service(context, fixedTimeProvider, out var userManager, out var tokenMintingService);

        var user = PostgresIntegrationFixture.CreateUser("logout.success@example.com");
        var created = await userManager.CreateAsync(user, "Password1!");
        Assert.True(created.Succeeded);

        var token = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "logout-refresh-hash",
            ExpiresAt = now.AddHours(1),
            CreatedByIp = "127.0.0.1",
            UserAgent = "IntegrationTests"
        };
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();

        tokenMintingService.HashToken("raw-logout-token").Returns("logout-refresh-hash");

        var result = await service.RevokeAsync(new LogoutCommand("raw-logout-token"));

        Assert.True(result.IsSuccess);

        await context.Entry(token).ReloadAsync();
        Assert.True(token.IsRevoked);
        Assert.NotNull(token.ReplacedAt);
    }

    [Fact]
    public async Task RefreshAsync_ReusingSameTokenAfterRotation_ReturnsRevoked()
    {
        var now = DateTimeOffset.UtcNow;
        var fixedTimeProvider = new FixedTimeProvider(now);

        await using var context = await fixture.CreateContextAsync(fixedTimeProvider);

        var service = CreateOAuth2Service(context, fixedTimeProvider, out var userManager, out var tokenMintingService);

        var user = PostgresIntegrationFixture.CreateUser("refresh.replay@example.com");
        var created = await userManager.CreateAsync(user, "Password1!");
        Assert.True(created.Succeeded);

        var addedRole = await userManager.AddToRoleAsync(user, "Customer");
        Assert.True(addedRole.Succeeded);

        var oldToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "replay-old-hash",
            ExpiresAt = now.AddHours(2),
            CreatedByIp = "127.0.0.1",
            UserAgent = "IntegrationTests"
        };
        context.RefreshTokens.Add(oldToken);
        await context.SaveChangesAsync();

        tokenMintingService.HashToken("raw-replay-token").Returns("replay-old-hash");
        tokenMintingService.MintRefreshToken()
            .Returns(("raw-new-refresh", "replay-new-hash", now.AddDays(7)));
        tokenMintingService.MintAccessToken(Arg.Any<User>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(("replay-access-token", now.AddMinutes(15)));

        var firstResult = await service.RefreshAsync(new RefreshCommand(
            "raw-replay-token",
            "10.0.0.2",
            "Mobile"));

        Assert.True(firstResult.IsSuccess);

        var replayResult = await service.RefreshAsync(new RefreshCommand(
            "raw-replay-token",
            "10.0.0.3",
            "ReplayClient"));

        Assert.True(replayResult.IsFailure);
        Assert.Equal("auth.refresh.revoked", replayResult.Error.Code);

        Assert.Equal(2, await context.RefreshTokens.CountAsync(rt => rt.UserId == user.Id));
    }

    private static OAuth2Service CreateOAuth2Service(
        JOrderIdentityDbContext context,
        TimeProvider timeProvider,
        out UserManager<User> userManager,
        out ITokenMintingService tokenMintingService)
    {
        var userStore = new UserStore<User, Role, JOrderIdentityDbContext, Guid>(context);
        userManager = IdentityIntegrationTestHelpers.CreateUserManager(userStore);

        tokenMintingService = Substitute.For<ITokenMintingService>();
        var refreshTokenService = new RefreshTokenService(context, tokenMintingService, timeProvider);

        return new OAuth2Service(
            userManager,
            tokenMintingService,
            refreshTokenService,
            context,
            timeProvider,
            Substitute.For<ILogger<OAuth2Service>>());
    }
}

