using JOrder.Identity.Contracts.Responses;
using JOrder.Identity.Controllers;
using JOrder.Identity.Options;
using JOrder.Identity.Services.Interfaces;
using JOrder.Testing.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Controllers;

public class WellKnownControllerUnitTests : ApiControllerUnitTestBase
{
    private readonly IOptions<JwtSigningOptions> _jwtSigningOptionsAccessor;
    private readonly ISigningKeyMaterialService _signingKeyMaterialService;
    private readonly WellKnownController _wellKnownController;

    public WellKnownControllerUnitTests()
    {
        _jwtSigningOptionsAccessor = Substitute.For<IOptions<JwtSigningOptions>>();
        _jwtSigningOptionsAccessor.Value.Returns(new JwtSigningOptions
        {
            Issuer = "https://localhost:5001",
            Algorithm = "RS256"
        });
        
        _signingKeyMaterialService = Substitute.For<ISigningKeyMaterialService>();
        var logger = Substitute.For<ILogger<WellKnownController>>();
        
        _wellKnownController = new WellKnownController(
            _jwtSigningOptionsAccessor,
            _signingKeyMaterialService,
            logger);
        
        AttachHttpContext(_wellKnownController, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");
    }
    [Fact]
    public void GetOpenIdConfiguration_ReturnsExpectedConfiguration()
    {
        // Arrange
        var configuredIssuer = _jwtSigningOptionsAccessor.Value.Issuer;
        var normalizedIssuer = configuredIssuer.TrimEnd('/');

        // Act
        var response = _wellKnownController.GetOpenIdConfiguration();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<OpenIdConfigurationResponseDto>(okResult.Value);

        Assert.Equal(configuredIssuer, payload.Issuer);
        Assert.Equal($"{normalizedIssuer}/.well-known/jwks.json", payload.JwksUri);
        Assert.Equal($"{normalizedIssuer}/oauth2/token", payload.TokenEndpoint);
        Assert.Equal($"{normalizedIssuer}/Users/me", payload.UserInfoEndpoint);
        Assert.Equal($"{normalizedIssuer}/oauth2/revoke", payload.RevocationEndpoint);
        Assert.Equal($"{normalizedIssuer}/Session/logout-all", payload.EndSessionEndpoint);
        Assert.Equal(["public"], payload.SubjectTypesSupported);
        Assert.Equal(["RS256"], payload.IdTokenSigningAlgValuesSupported);
        Assert.Equal(["password", "refresh_token"], payload.GrantTypesSupported);
        Assert.Empty(payload.ResponseTypesSupported);
        Assert.Equal(["openid", "profile", "email", "roles", "offline_access"], payload.ScopesSupported);
        Assert.Equal(["none"], payload.TokenEndpointAuthMethodsSupported);
        Assert.Equal(
            [
                JwtRegisteredClaimNames.Sub,
                JwtRegisteredClaimNames.Email,
                "role"
            ],
            payload.ClaimsSupported);
    }

    [Fact]
    public void GetOpenIdConfiguration_WithTrailingSlashIssuer_UsesTrimmedIssuerForEndpointUrls()
    {
        // Arrange
        var configuredIssuer = "https://issuer.example.com/";
        _jwtSigningOptionsAccessor.Value.Returns(new JwtSigningOptions
        {
            Issuer = configuredIssuer,
            Algorithm = "ES256"
        });
        var normalizedIssuer = configuredIssuer.TrimEnd('/');

        // Act
        var response = _wellKnownController.GetOpenIdConfiguration();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<OpenIdConfigurationResponseDto>(okResult.Value);

        Assert.Equal(configuredIssuer, payload.Issuer);
        Assert.Equal($"{normalizedIssuer}/.well-known/jwks.json", payload.JwksUri);
        Assert.Equal($"{normalizedIssuer}/oauth2/token", payload.TokenEndpoint);
        Assert.Equal(["ES256"], payload.IdTokenSigningAlgValuesSupported);
    }

    [Fact]
    public void GetJsonWebKeySet_ReturnsPublicKeyFromSigningService()
    {
        // Arrange
        var publicKey = new JsonWebKey
        {
            Kid = "key-1",
            Kty = "RSA",
            Use = "sig",
            E = "AQAB",
            N = "test-modulus"
        };
        _signingKeyMaterialService.GetPublicJsonWebKey().Returns(publicKey);

        // Act
        var response = _wellKnownController.GetJsonWebKeySet();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<JsonWebKeySetResponseDto>(okResult.Value);
        var key = Assert.Single(payload.Keys);

        Assert.Same(publicKey, key);
        _signingKeyMaterialService.Received(1).GetPublicJsonWebKey();
    }
}