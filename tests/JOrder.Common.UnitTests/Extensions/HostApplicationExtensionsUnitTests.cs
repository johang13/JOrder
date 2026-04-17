using System.Reflection;
using System.Security.Cryptography;
using JOrder.Common.Attributes;
using JOrder.Common.Extensions;
using JOrder.Common.Options;
using JOrder.Common.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
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
    public void AddJOrderCommon_WhenWebApplicationBuilder_RegistersControllersAndOpenApi()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{ServiceOptions.SectionName}:Name"] = "svc",
            [$"{ServiceOptions.SectionName}:Version"] = "1.0.0"
        });

        builder.AddJOrderCommon();

        using var provider = builder.Services.BuildServiceProvider();

        Assert.Contains(builder.Services, d => d.ServiceType.FullName == "Microsoft.AspNetCore.Mvc.Infrastructure.IActionDescriptorCollectionProvider");
        Assert.Contains(builder.Services, d => d.ServiceType.FullName?.Contains("OpenApi", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void AddJOrderRateLimiting_RegistersGlobalLimiterConfiguration()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddJOrderRateLimiting();

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        Assert.Equal(429, options.RejectionStatusCode);
        Assert.NotNull(options.OnRejected);
        Assert.NotNull(options.GlobalLimiter);
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
    public void AddJOrderServicesFromAssembly_IgnoresUnattributedTypes_AndFrameworkInterfaces()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddJOrderServicesFromAssembly(Assembly.GetExecutingAssembly());

        Assert.DoesNotContain(builder.Services,
            d => d.ServiceType == typeof(IUnattributedService) && d.ImplementationType == typeof(UnattributedService));
        Assert.DoesNotContain(builder.Services,
            d => d.ServiceType == typeof(IDisposable) && d.ImplementationType == typeof(MixedInterfaceService));
        Assert.Contains(builder.Services,
            d => d.ServiceType == typeof(IMixedInterfaceService) && d.ImplementationType == typeof(MixedInterfaceService));
    }

    [Fact]
    public void AddJOrderJwtValidation_ConfiguresJwtBearerOptionsFromConfiguration()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{JwtValidationOptions.SectionName}:Authority"] = "https://identity.example.local",
            [$"{JwtValidationOptions.SectionName}:Audience"] = "jorder-api",
            [$"{JwtValidationOptions.SectionName}:RequireHttpsMetadata"] = "false"
        });

        builder.AddJOrderJwtValidation(options => options.SaveToken = true);

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("https://identity.example.local", options.Authority);
        Assert.Equal("jorder-api", options.Audience);
        Assert.False(options.RequireHttpsMetadata);
        Assert.True(options.SaveToken);
    }

    [Fact]
    public void AddJOrderJwtAuthentication_AppliesCustomConfiguration()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddJOrderJwtAuthentication(options => options.SaveToken = true);

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.True(options.SaveToken);
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
    public interface IMixedInterfaceService;
    public interface IUnattributedService;

    [ScopedService]
    public sealed class TestScopedService : ITestScopedService;

    [TransientService]
    public sealed class TestTransientService : ITestTransientService;

    [SingletonService]
    public sealed class TestSingletonService : ITestSingletonService;

    [ScopedService]
    public sealed class SelfRegisteredService;

    [TransientService]
    public sealed class MixedInterfaceService : IMixedInterfaceService, IDisposable
    {
        public void Dispose()
        {
        }
    }

    public sealed class UnattributedService : IUnattributedService;

    private sealed class StubCurrentUser : ICurrentUser
    {
        public Guid? Id => null;
        public string? Email => null;
        public bool IsAuthenticated => false;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}
