using JOrder.Common.Models.Interfaces;
using JOrder.Common.Options;
using JOrder.Common.Persistence;
using JOrder.Common.Services.Interfaces;
using JOrder.Testing.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace JOrder.Common.UnitTests.Persistence;

public class AuditableInterceptorUnitTests
{
    [Fact]
    public async Task SaveChanges_WhenEntityAdded_SetsCreatedFields()
    {
        var now = new DateTimeOffset(2026, 4, 17, 12, 0, 0, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var currentUser = BuildCurrentUserMock(userId, "chris@example.com", isAuthenticated: true);
        var interceptor = BuildInterceptor("identity-service", currentUser, now);

        await using var context = BuildDbContext(interceptor, dbName: Guid.NewGuid().ToString());
        var entity = new TestAuditableModel { Name = "new" };
        context.Entities.Add(entity);

        await context.SaveChangesAsync();

        Assert.Equal(now, entity.CreatedAt);
        Assert.Equal(userId, entity.CreatedById);
        Assert.Equal("chris@example.com", entity.CreatedBy);
        Assert.Null(entity.UpdatedAt);
        Assert.Null(entity.UpdatedById);
        Assert.Null(entity.UpdatedBy);
    }

    [Fact]
    public async Task SaveChanges_WhenEntityModified_SetsUpdatedFields_AndKeepsCreatedFields()
    {
        var originalCreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var updateTime = new DateTimeOffset(2026, 4, 17, 13, 0, 0, TimeSpan.Zero);
        var creatorId = Guid.NewGuid();
        var updaterId = Guid.NewGuid();

        var createUser = BuildCurrentUserMock(creatorId, "creator@example.com", isAuthenticated: true);
        var createInterceptor = BuildInterceptor("identity-service", createUser, originalCreatedAt);

        var dbName = Guid.NewGuid().ToString();
        await using (var seedContext = BuildDbContext(createInterceptor, dbName))
        {
            seedContext.Entities.Add(new TestAuditableModel { Name = "seed" });
            await seedContext.SaveChangesAsync();
        }

        var updateUser = BuildCurrentUserMock(updaterId, "updater@example.com", isAuthenticated: true);
        var updateInterceptor = BuildInterceptor("identity-service", updateUser, updateTime);

        await using var updateContext = BuildDbContext(updateInterceptor, dbName);
        var entity = await updateContext.Entities.SingleAsync();

        // Simulate accidental mutation of created fields; interceptor should preserve persisted values.
        entity.CreatedAt = updateTime.AddDays(1);
        entity.CreatedBy = "tampered@example.com";
        entity.CreatedById = Guid.NewGuid();
        entity.Name = "updated";

        await updateContext.SaveChangesAsync();

        Assert.Equal(originalCreatedAt, entity.CreatedAt);
        Assert.Equal(creatorId, entity.CreatedById);
        Assert.Equal("creator@example.com", entity.CreatedBy);
        Assert.Equal(updateTime, entity.UpdatedAt);
        Assert.Equal(updaterId, entity.UpdatedById);
        Assert.Equal("updater@example.com", entity.UpdatedBy);
    }

    [Fact]
    public async Task SaveChanges_WhenUserIsAnonymous_UsesServiceNameAsActor()
    {
        var now = new DateTimeOffset(2026, 4, 17, 14, 0, 0, TimeSpan.Zero);
        var currentUser = BuildCurrentUserMock(id: null, email: null, isAuthenticated: false);
        var interceptor = BuildInterceptor("jorder-identity", currentUser, now);

        await using var context = BuildDbContext(interceptor, dbName: Guid.NewGuid().ToString());
        var entity = new TestAuditableModel { Name = "anonymous-write" };
        context.Entities.Add(entity);

        await context.SaveChangesAsync();

        Assert.Equal("jorder-identity", entity.CreatedBy);
        Assert.Null(entity.CreatedById);
    }

    private static AuditableInterceptor BuildInterceptor(string serviceName, ICurrentUser currentUser, DateTimeOffset now)
    {
        var serviceOptions = Microsoft.Extensions.Options.Options.Create(new ServiceOptions { Name = serviceName, Version = "1.0.0" });
        return new AuditableInterceptor(serviceOptions, currentUser, new FixedTimeProvider(now), NullLogger<AuditableInterceptor>.Instance);
    }

    private static TestDbContext BuildDbContext(AuditableInterceptor interceptor, string dbName)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .Options;

        return new TestDbContext(options);
    }

    private static ICurrentUser BuildCurrentUserMock(Guid? id, string? email, bool isAuthenticated)
    {
        var mock = Substitute.For<ICurrentUser>();
        mock.Id.Returns(id);
        mock.Email.Returns(email);
        mock.IsAuthenticated.Returns(isAuthenticated);
        return mock;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestAuditableModel> Entities => Set<TestAuditableModel>();
    }

    private sealed class TestAuditableModel : IAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public Guid? CreatedById { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public Guid? UpdatedById { get; set; }
        public string? UpdatedBy { get; set; }
    }

}

