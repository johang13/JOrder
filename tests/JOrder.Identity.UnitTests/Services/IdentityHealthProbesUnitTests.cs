using JOrder.Identity.Persistence;
using JOrder.Identity.Services;
using JOrder.Identity.Services.Interfaces;
using JOrder.Identity.UnitTests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Services;

public class IdentityHealthProbesUnitTests
{
    [Fact]
    public async Task StartupProbe_WhenDependenciesHealthy_Completes()
    {
        // Arrange
        var signingKeyMaterialService = Substitute.For<ISigningKeyMaterialService>();

        var (context, connection) = IdentityTestHelpers.CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

        var probes = new IdentityHealthProbes(signingKeyMaterialService, context);

        // Act
        await probes.StartupProbe();

        // Assert
        signingKeyMaterialService.Received(1).GetSigningKey();
    }

    [Fact]
    public async Task StartupProbe_WhenDatabaseUnavailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var signingKeyMaterialService = Substitute.For<ISigningKeyMaterialService>();

        var options = new DbContextOptionsBuilder<JOrderIdentityDbContext>()
            .UseSqlite(new SqliteConnection("Data Source=/definitely-missing-folder/jorder.db"))
            .Options;

        await using var context = new JOrderIdentityDbContext(options);
        var probes = new IdentityHealthProbes(signingKeyMaterialService, context);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => probes.StartupProbe());
    }

    [Fact]
    public async Task LivenessProbe_AlwaysCompletes()
    {
        // Arrange
        var (context, connection) = IdentityTestHelpers.CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

        var probes = new IdentityHealthProbes(Substitute.For<ISigningKeyMaterialService>(), context);

        // Act
        await probes.LivenessProbe();

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task ReadinessProbe_WhenDependenciesHealthy_Completes()
    {
        // Arrange
        var signingKeyMaterialService = Substitute.For<ISigningKeyMaterialService>();

        var (context, connection) = IdentityTestHelpers.CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

        var probes = new IdentityHealthProbes(signingKeyMaterialService, context);

        // Act
        await probes.ReadinessProbe();

        // Assert
        signingKeyMaterialService.DidNotReceive().GetSigningKey();
    }

    [Fact]
    public async Task ReadinessProbe_WhenDatabaseUnavailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var signingKeyMaterialService = Substitute.For<ISigningKeyMaterialService>();

        var options = new DbContextOptionsBuilder<JOrderIdentityDbContext>()
            .UseSqlite(new SqliteConnection("Data Source=/definitely-missing-folder/jorder.db"))
            .Options;

        await using var context = new JOrderIdentityDbContext(options);
        var probes = new IdentityHealthProbes(signingKeyMaterialService, context);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => probes.ReadinessProbe());
    }
}

