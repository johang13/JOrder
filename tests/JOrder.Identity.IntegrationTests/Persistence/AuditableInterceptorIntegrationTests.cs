using JOrder.Identity.IntegrationTests.TestInfrastructure;
using JOrder.Identity.Models;
using JOrder.Testing.Time;

namespace JOrder.Identity.IntegrationTests.Persistence;

[Collection(IdentityPostgresIntegrationCollection.Name)]
public sealed class AuditableInterceptorIntegrationTests(PostgresIntegrationFixture fixture)
{
    [Fact]
    public async Task SaveChanges_ForAnonymousActor_StampsCreatedAndUpdatedWithServiceName()
    {
        var now = new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero);
        var fixedTimeProvider = new FixedTimeProvider(now);

        await using var context = await fixture.CreateContextAsync(fixedTimeProvider);

        var user = PostgresIntegrationFixture.CreateUser("auditable.anon@example.com");
        context.Users.Add(user);

        var token = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "auditable-anon-hash",
            ExpiresAt = now.AddHours(1),
            CreatedByIp = "127.0.0.1",
            UserAgent = "IntegrationTests"
        };

        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();

        Assert.Equal(now, token.CreatedAt);
        Assert.Equal("IdentityIntegrationTests", token.CreatedBy);
        Assert.Null(token.CreatedById);

        var createdAtBeforeUpdate = token.CreatedAt;
        var createdByBeforeUpdate = token.CreatedBy;

        token.UserAgent = "UpdatedAgent";
        await context.SaveChangesAsync();

        Assert.Equal(createdAtBeforeUpdate, token.CreatedAt);
        Assert.Equal(createdByBeforeUpdate, token.CreatedBy);
        Assert.Equal(now, token.UpdatedAt);
        Assert.Equal("IdentityIntegrationTests", token.UpdatedBy);
        Assert.Null(token.UpdatedById);
    }

    [Fact]
    public async Task SaveChanges_ForAuthenticatedActor_StampsCreatedByEmailAndId()
    {
        var now = new DateTimeOffset(2026, 4, 20, 13, 0, 0, TimeSpan.Zero);
        var actorId = Guid.NewGuid();
        var actorEmail = "auditor@example.com";

        await using var context = await fixture.CreateContextAsync(
            new FixedTimeProvider(now),
            isAuthenticated: true,
            currentUserId: actorId,
            currentUserEmail: actorEmail);

        var user = PostgresIntegrationFixture.CreateUser("auditable.auth@example.com");
        context.Users.Add(user);

        var token = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "auditable-auth-hash",
            ExpiresAt = now.AddHours(1),
            CreatedByIp = "127.0.0.1",
            UserAgent = "IntegrationTests"
        };

        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();

        Assert.Equal(now, token.CreatedAt);
        Assert.Equal(actorId, token.CreatedById);
        Assert.Equal(actorEmail, token.CreatedBy);
    }
}
