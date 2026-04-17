using System.Reflection;
using System.Security.Cryptography;
using JOrder.Common.Attributes;
using JOrder.Common.Extensions;
using JOrder.Common.Options;
using JOrder.Common.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JOrder.Common.UnitTests.Extensions;

public class HostApplicationExtensionsUnitTests
{
    [Fact]
    public void AddJOrderCommon_RegistersCoreInfrastructure()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{ServiceOptions.SectionName}:Name"] = "svc",
            [$"{ServiceOptions.SectionName}:Version"] = "1.0.0"
        });

        builder.AddJOrderCommon();

        using var provider = builder.Services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<TimeProvider>());
        Assert.NotNull(provider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>());
        Assert.NotNull(provider.GetService<ICurrentUser>());
    }

    [Fact]
    public void AddJOrderWarmupTask_DoesNotDuplicateSameTaskType()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddJOrderWarmupTask<TestWarmupTask>();
        builder.AddJOrderWarmupTask<TestWarmupTask>();

        var registrations = builder.Services
            .Where(d => d.ServiceType == typeof(IJOrderWarmupTask) && d.ImplementationType == typeof(TestWarmupTask))
            .ToList();

        Assert.Single(registrations);
    }

    [Fact]
    public void AddJOrderServicesFromAssembly_RegistersAttributedTypesWithExpectedLifetime()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddJOrderServicesFromAssembly(Assembly.GetExecutingAssembly());

        using var provider = builder.Services.BuildServiceProvider();

        var scoped = provider.GetRequiredService<ITestScopedService>();
        var transient1 = provider.GetRequiredService<ITestTransientService>();
        var transient2 = provider.GetRequiredService<ITestTransientService>();
        var singleton1 = provider.GetRequiredService<ITestSingletonService>();
        var singleton2 = provider.GetRequiredService<ITestSingletonService>();
        var selfRegistered = provider.GetRequiredService<SelfRegisteredService>();

        Assert.IsType<TestScopedService>(scoped);
        Assert.IsType<TestTransientService>(transient1);
        Assert.IsType<TestSingletonService>(singleton1);
        Assert.IsType<SelfRegisteredService>(selfRegistered);

        Assert.NotSame(transient1, transient2);
        Assert.Same(singleton1, singleton2);
    }

    [Fact]
    public void AddJOrderJwtIssuerAuthentication_ConfiguresJwtBearerOptions()
    {
        using var rsa = RSA.Create();
        var key = new RsaSecurityKey(rsa);
        var builder = Host.CreateApplicationBuilder();

        builder.AddJOrderJwtIssuerAuthentication("issuer-a", "audience-a", key);

        using var provider = builder.Services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var options = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.NotNull(options.TokenValidationParameters);
        Assert.Equal("issuer-a", options.TokenValidationParameters.ValidIssuer);
        Assert.Equal("audience-a", options.TokenValidationParameters.ValidAudience);
        Assert.Equal(key, options.TokenValidationParameters.IssuerSigningKey);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
    }

    [Fact]
    public void AddJOrderDatabase_RegistersDbContextAndInterceptorPipeline()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{DatabaseOptions.SectionName}:Provider"] = "InMemory",
            [$"{DatabaseOptions.SectionName}:ConnectionString"] = "jorder-common-unit-tests"
        });

        builder.Services.AddSingleton(
            Microsoft.Extensions.Options.Options.Create(new ServiceOptions { Name = "svc", Version = "1.0.0" }));
        builder.Services.AddScoped<ICurrentUser>(_ => new StubCurrentUser());
        builder.Services.AddSingleton(TimeProvider.System);

        builder.AddJOrderDatabase<TestDbContext>((_, options, dbOptions) =>
            options.UseInMemoryDatabase(dbOptions.ConnectionString));

        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        Assert.NotNull(context);
    }

    public sealed class TestWarmupTask : IJOrderWarmupTask
    {
        public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public interface ITestScopedService;
    public interface ITestTransientService;
    public interface ITestSingletonService;

    [ScopedService]
    public sealed class TestScopedService : ITestScopedService;

    [TransientService]
    public sealed class TestTransientService : ITestTransientService;

    [SingletonService]
    public sealed class TestSingletonService : ITestSingletonService;

    [ScopedService]
    public sealed class SelfRegisteredService;

    private sealed class StubCurrentUser : ICurrentUser
    {
        public Guid? Id => null;
        public string? Email => null;
        public bool IsAuthenticated => false;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}


