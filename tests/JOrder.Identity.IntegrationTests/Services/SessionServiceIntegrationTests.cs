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
public sealed class SessionServiceIntegrationTests(PostgresIntegrationFixture fixture)
{
    [Fact]
    public async Task LogoutAllAsync_Success_RevokesActiveTokensForUser()
    {
        var now = DateTimeOffset.UtcNow;
        var fixedTimeProvider = new FixedTimeProvider(now);

        await using var context = await fixture.CreateContextAsync(fixedTimeProvider);

        var userStore = new UserStore<User, Role, JOrderIdentityDbContext, Guid>(context);
        var userManager = IdentityIntegrationTestHelpers.CreateUserManager(userStore);

        var user = PostgresIntegrationFixture.CreateUser("logout-all@example.com");
        var createResult = await userManager.CreateAsync(user, "Password1!");
        Assert.True(createResult.Succeeded);

        context.RefreshTokens.AddRange(
            new RefreshToken
            {
                UserId = user.Id,
                TokenHash = "active",
                ExpiresAt = now.AddHours(2),
                CreatedByIp = "127.0.0.1",
                UserAgent = "IntegrationTests"
            },
            new RefreshToken
            {
                UserId = user.Id,
                TokenHash = "expired",
                ExpiresAt = now.AddHours(-2),
                CreatedByIp = "127.0.0.1",
                UserAgent = "IntegrationTests"
            });
        await context.SaveChangesAsync();

        var tokenMintingService = Substitute.For<ITokenMintingService>();
        var refreshTokenService = new RefreshTokenService(context, tokenMintingService, fixedTimeProvider);

        var service = new SessionService(
            userManager,
            refreshTokenService,
            Substitute.For<ILogger<SessionService>>());

        var result = await service.LogoutAllAsync(new LogoutAllCommand(user.Id));

        Assert.True(result.IsSuccess);

        var tokens = context.RefreshTokens
            .AsNoTracking()
            .Where(t => t.UserId == user.Id)
            .OrderBy(t => t.TokenHash)
            .ToArray();
        var active = tokens.Single(t => t.TokenHash == "active");
        var expired = tokens.Single(t => t.TokenHash == "expired");

        Assert.True(active.IsRevoked);
        Assert.NotNull(active.ReplacedAt);
        Assert.False(expired.IsRevoked);
    }
}


