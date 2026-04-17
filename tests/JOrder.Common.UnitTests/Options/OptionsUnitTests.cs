using System.ComponentModel.DataAnnotations;
using JOrder.Common.Options;

namespace JOrder.Common.UnitTests.Options;

public class OptionsUnitTests
{
    [Fact]
    public void SectionNames_AreStable()
    {
        Assert.Equal("JOrder:DatabaseOptions", DatabaseOptions.SectionName);
        Assert.Equal("JOrder:ServiceOptions", ServiceOptions.SectionName);
        Assert.Equal("JOrder:Authentication:JwtValidation", JwtValidationOptions.SectionName);
    }

    [Fact]
    public void DatabaseOptions_RequiresProviderAndConnectionString()
    {
        var options = new DatabaseOptions();

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), [], true);

        Assert.False(isValid);
    }

    [Fact]
    public void DatabaseOptions_WithRequiredFields_PassesValidation()
    {
        var options = new DatabaseOptions { Provider = "Postgres", ConnectionString = "Host=localhost" };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), [], true);

        Assert.True(isValid);
    }

    [Fact]
    public void JwtValidationOptions_ValidatesAuthorityAsUrl()
    {
        var options = new JwtValidationOptions { Authority = "not-url", Audience = "api" };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(JwtValidationOptions.Authority)));
    }

    [Fact]
    public void JwtValidationOptions_WithValidFields_PassesValidation()
    {
        var options = new JwtValidationOptions { Authority = "https://identity.local", Audience = "api" };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), [], true);

        Assert.True(isValid);
    }

    [Fact]
    public void ServiceOptions_RequiresNameAndVersion()
    {
        var options = new ServiceOptions();

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), [], true);

        Assert.False(isValid);
    }

    [Fact]
    public void ServiceOptions_WithRequiredFields_PassesValidation()
    {
        var options = new ServiceOptions { Name = "identity", Version = "1.0.0" };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), [], true);

        Assert.True(isValid);
    }
}
