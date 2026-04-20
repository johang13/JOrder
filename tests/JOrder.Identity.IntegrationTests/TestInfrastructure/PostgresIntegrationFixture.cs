using JOrder.Common.Options;
using JOrder.Common.Persistence;
using JOrder.Common.Services.Interfaces;
using JOrder.Identity.Models;
using JOrder.Identity.Persistence;
using JOrder.Testing.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace JOrder.Identity.IntegrationTests.TestInfrastructure;

public sealed class PostgresIntegrationFixture : PostgreSqlContainerFixtureBase, IAsyncLifetime
{
    public PostgresIntegrationFixture()
        : base(database: "jorder_identity_tests")
    {
    }

    public async Task<JOrderIdentityDbContext> CreateContextAsync(
        TimeProvider timeProvider,
        bool isAuthenticated = false,
        Guid? currentUserId = null,
        string? currentUserEmail = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(isAuthenticated);
        currentUser.Id.Returns(currentUserId);
        currentUser.Email.Returns(currentUserEmail);

        var options = Microsoft.Extensions.Options.Options.Create(new ServiceOptions
        {
            Name = "IdentityIntegrationTests",
            Version = "1.0.0"
        });

        var interceptor = new AuditableInterceptor(
            options,
            currentUser,
            timeProvider,
            Substitute.For<ILogger<AuditableInterceptor>>());

        var dbOptions = new DbContextOptionsBuilder<JOrderIdentityDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        var context = new JOrderIdentityDbContext(dbOptions);

        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        return context;
    }

    public static User CreateUser(string email = "user@example.com")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            FirstName = "Test",
            LastName = "User"
        };
    }
}


