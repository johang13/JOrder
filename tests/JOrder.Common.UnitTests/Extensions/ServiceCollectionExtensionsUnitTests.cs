using JOrder.Common.Extensions;
using JOrder.Common.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JOrder.Common.UnitTests.Extensions;

public class ServiceCollectionExtensionsUnitTests
{
    [Fact]
    public void AddJwtAuthentication_RegistersAuthenticationAndAuthorization()
    {
        var services = new ServiceCollection();

        var result = services.AddJwtAuthentication(_ => { });

        Assert.Same(services, result);
        Assert.Contains(services, d => d.ServiceType == typeof(IConfigureOptions<JwtBearerOptions>));
        Assert.Contains(services, d => d.ServiceType.FullName == "Microsoft.AspNetCore.Authorization.IAuthorizationService");
    }

    [Fact]
    public void AddJwtValidation_BindsConfiguredOptions_AndRunsCustomConfiguration()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{JwtValidationOptions.SectionName}:Authority"] = "https://identity.jorder.local",
            [$"{JwtValidationOptions.SectionName}:Audience"] = "jorder-api",
            [$"{JwtValidationOptions.SectionName}:RequireHttpsMetadata"] = "false"
        });

        var services = builder.Services;
        var returned = services.AddJwtValidation(opts => opts.SaveToken = true);

        Assert.Same(services, returned);

        using var provider = services.BuildServiceProvider();
        var jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        var validationOptions = provider.GetRequiredService<IOptions<JwtValidationOptions>>().Value;

        Assert.Equal("https://identity.jorder.local", validationOptions.Authority);
        Assert.Equal("jorder-api", validationOptions.Audience);
        Assert.False(validationOptions.RequireHttpsMetadata);

        Assert.Equal(validationOptions.Authority, jwtOptions.Authority);
        Assert.Equal(validationOptions.Audience, jwtOptions.Audience);
        Assert.False(jwtOptions.RequireHttpsMetadata);
        Assert.True(jwtOptions.SaveToken);
    }

    [Fact]
    public void AddJOrderOptions_RegistersTypedOptionsBinding()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{ServiceOptions.SectionName}:Name"] = "svc",
            [$"{ServiceOptions.SectionName}:Version"] = "1.2.3"
        });

        var services = builder.Services;
        var returned = services.AddJOrderOptions<ServiceOptions>();

        Assert.Same(services, returned);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ServiceOptions>>().Value;

        Assert.Equal("svc", options.Name);
        Assert.Equal("1.2.3", options.Version);
    }

    [Fact]
    public void AddJOrderOpenApi_AddsOpenApiServices()
    {
        var services = new ServiceCollection();

        var beforeCount = services.Count;
        var returned = services.AddJOrderOpenApi("JOrder API");

        Assert.Same(services, returned);
        Assert.True(services.Count > beforeCount);
        Assert.Contains(services, d => d.ServiceType.FullName?.Contains("OpenApi", StringComparison.Ordinal) == true);
    }
}
