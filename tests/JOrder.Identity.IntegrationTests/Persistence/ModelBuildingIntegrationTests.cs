using JOrder.Identity.IntegrationTests.TestInfrastructure;
using JOrder.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace JOrder.Identity.IntegrationTests.Persistence;

[Collection(IdentityPostgresIntegrationCollection.Name)]
public sealed class ModelBuildingIntegrationTests(PostgresIntegrationFixture fixture)
{
    [Fact]
    public async Task Database_ContainsSeededRoles()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);

        var roleNames = await context.Roles
            .Select(r => r.Name!)
            .OrderBy(n => n)
            .ToArrayAsync();

        Assert.Equal(["Admin", "Customer", "Employee", "Manager"], roleNames);
    }

    [Fact]
    public async Task RefreshTokenTokenHash_MustBeUnique()
    {
        await using var context = await fixture.CreateContextAsync(TimeProvider.System);

        var user = PostgresIntegrationFixture.CreateUser();
        context.Users.Add(user);

        context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "duplicate-hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            CreatedByIp = "127.0.0.1",
            UserAgent = "IntegrationTests"
        });

        await context.SaveChangesAsync();

        context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "duplicate-hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(2),
            CreatedByIp = "127.0.0.1",
            UserAgent = "IntegrationTests"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}

