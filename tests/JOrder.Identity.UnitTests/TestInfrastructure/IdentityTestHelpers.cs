using JOrder.Common.Options;
using JOrder.Common.Persistence;
using JOrder.Common.Services.Interfaces;
using JOrder.Identity.Models;
using JOrder.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace JOrder.Identity.UnitTests.TestInfrastructure;

internal static class IdentityTestHelpers
{
    public static UserManager<User> CreateUserManager()
    {
        var store = Substitute.For<IUserStore<User>>();
        return Substitute.For<UserManager<User>>(
            store,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }

    public static (JOrderIdentityDbContext Context, SqliteConnection Connection) CreateSqliteContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(false);

        var serviceOptions = Microsoft.Extensions.Options.Options.Create(new ServiceOptions
        {
            Name = "IdentityTests",
            Version = "1.0.0"
        });

        var interceptorLogger = Substitute.For<ILogger<AuditableInterceptor>>();
        var auditableInterceptor = new AuditableInterceptor(
            serviceOptions,
            currentUser,
            TimeProvider.System,
            interceptorLogger);

        var options = new DbContextOptionsBuilder<JOrderIdentityDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(auditableInterceptor)
            .Options;

        var context = new JOrderIdentityDbContext(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }
}



