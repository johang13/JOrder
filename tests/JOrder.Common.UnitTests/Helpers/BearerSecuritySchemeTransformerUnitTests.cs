using JOrder.Common.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using NSubstitute;

namespace JOrder.Common.UnitTests.Helpers;

public class BearerSecuritySchemeTransformerUnitTests
{
    [Fact]
    public async Task TransformAsync_WhenBearerSchemeExists_AddsBearerSecurityScheme()
    {
        // Arrange
        var authenticationSchemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        authenticationSchemeProvider.GetAllSchemesAsync().Returns(
        [
            new AuthenticationScheme("Bearer", "Bearer", typeof(TestAuthHandler))
        ]);

        var transformer = new BearerSecuritySchemeTransformer(authenticationSchemeProvider);
        var document = new OpenApiDocument();

        // Act
        await transformer.TransformAsync(document, null!, CancellationToken.None);

        // Assert
        Assert.NotNull(document.Components);
        Assert.NotNull(document.Components.SecuritySchemes);
        Assert.True(document.Components.SecuritySchemes.ContainsKey("Bearer"));

        var bearerScheme = Assert.IsType<OpenApiSecurityScheme>(document.Components.SecuritySchemes["Bearer"]);
        Assert.Equal(SecuritySchemeType.Http, bearerScheme.Type);
        Assert.Equal("bearer", bearerScheme.Scheme);
        Assert.Equal(ParameterLocation.Header, bearerScheme.In);
        Assert.Equal("Json Web Token", bearerScheme.BearerFormat);
    }

    [Fact]
    public async Task TransformAsync_WhenBearerSchemeDoesNotExist_DoesNotModifyDocument()
    {
        // Arrange
        var authenticationSchemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        authenticationSchemeProvider.GetAllSchemesAsync().Returns(
        [
            new AuthenticationScheme("Cookies", "Cookies", typeof(TestAuthHandler))
        ]);

        var transformer = new BearerSecuritySchemeTransformer(authenticationSchemeProvider);
        var existingSecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["ApiKey"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, Name = "X-Api-Key", In = ParameterLocation.Header }
        };

        var document = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                SecuritySchemes = existingSecuritySchemes
            }
        };

        // Act
        await transformer.TransformAsync(document, null!, CancellationToken.None);

        // Assert
        Assert.NotNull(document.Components);
        Assert.Same(existingSecuritySchemes, document.Components.SecuritySchemes);
        Assert.True(document.Components.SecuritySchemes.ContainsKey("ApiKey"));
        Assert.False(document.Components.SecuritySchemes.ContainsKey("Bearer"));
    }

    private sealed class TestAuthHandler : IAuthenticationHandler
    {
        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => Task.CompletedTask;

        public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
