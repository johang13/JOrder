using JOrder.Identity.IntegrationTests.TestInfrastructure;
using JOrder.Identity.Models;
using JOrder.Identity.Services;
using JOrder.Identity.Services.Interfaces;
using JOrder.Testing.Time;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace JOrder.Identity.IntegrationTests.Services;

[Collection(IdentityPostgresIntegrationCollection.Name)]
public sealed class RefreshTokenServiceIntegrationTests(PostgresIntegrationFixture fixture)
{
    [Fact]
    public async Task SaveAsync_ThenFindByRawToken_RoundTripsThroughDatabase()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);

        var user = PostgresIntegrationFixture.CreateUser();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tokenMintingService = Substitute.For<ITokenMintingService>();
        tokenMintingService.HashToken("raw-token").Returns("hash-1");

        var service = new RefreshTokenService(context, tokenMintingService, TimeProvider.System);

        await service.SaveAsync(user.Id, "hash-1", DateTimeOffset.UtcNow.AddDays(7), "127.0.0.1", "IntegrationTests");

        var token = await service.FindByRawTokenAsync("raw-token");

        Assert.NotNull(token);
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal("hash-1", token.TokenHash);
    }

    [Fact]
    public async Task RotateAsync_RevokesOldToken_AndPersistsReplacement()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);

        var user = PostgresIntegrationFixture.CreateUser();
        context.Users.Add(user);

        var oldToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "old-hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            CreatedByIp = "127.0.0.1",
            UserAgent = "IntegrationTests"
        };

        context.RefreshTokens.Add(oldToken);
        await context.SaveChangesAsync();

        var service = new RefreshTokenService(
            context,
            Substitute.For<ITokenMintingService>(),
            TimeProvider.System);

        var newToken = await service.RotateAsync(oldToken, "new-hash", DateTimeOffset.UtcNow.AddDays(7), "10.0.0.2", "Mobile");

        await context.Entry(oldToken).ReloadAsync();

        Assert.True(oldToken.IsRevoked);
        Assert.Equal(newToken.Id, oldToken.ReplacedByTokenId);

        var persistedNew = await context.RefreshTokens.SingleAsync(rt => rt.Id == newToken.Id);
        Assert.Equal("new-hash", persistedNew.TokenHash);
        Assert.Equal("10.0.0.2", persistedNew.CreatedByIp);
    }

    [Fact]
    public async Task RevokeAllAsync_RevokesOnlyActiveUnexpiredTokensForUser()
    {
        var now = DateTimeOffset.UtcNow;
        var fixedTimeProvider = new FixedTimeProvider(now);

        await using var context = await fixture.CreateContextAsync(fixedTimeProvider);

        var user = PostgresIntegrationFixture.CreateUser("main@example.com");
        var otherUser = PostgresIntegrationFixture.CreateUser("other@example.com");

        context.Users.AddRange(user, otherUser);

        var active = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "active",
            ExpiresAt = now.AddHours(2),
            CreatedByIp = "127.0.0.1",
            UserAgent = "IntegrationTests"
        };

        var expired = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "expired",
            ExpiresAt = now.AddHours(-2),
            CreatedByIp = "127.0.0.1",
            UserAgent = "IntegrationTests"
        };

        var alreadyRevoked = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "revoked",
            ExpiresAt = now.AddHours(2),
            IsRevoked = true,
            CreatedByIp = "127.0.0.1",
            UserAgent = "IntegrationTests"
        };

        var otherUserToken = new RefreshToken
        {
            UserId = otherUser.Id,
            TokenHash = "other-user-token",
            ExpiresAt = now.AddHours(2),
            CreatedByIp = "127.0.0.1",
            UserAgent = "IntegrationTests"
        };

        context.RefreshTokens.AddRange(active, expired, alreadyRevoked, otherUserToken);
        await context.SaveChangesAsync();

        var service = new RefreshTokenService(
            context,
            Substitute.For<ITokenMintingService>(),
            fixedTimeProvider);

        await service.RevokeAllAsync(user);

        await context.Entry(active).ReloadAsync();
        await context.Entry(expired).ReloadAsync();
        await context.Entry(alreadyRevoked).ReloadAsync();
        await context.Entry(otherUserToken).ReloadAsync();

        Assert.True(active.IsRevoked);
        Assert.NotNull(active.ReplacedAt);
        Assert.False(expired.IsRevoked);
        Assert.True(alreadyRevoked.IsRevoked);
        Assert.False(otherUserToken.IsRevoked);
    }

}


