using System.Security.Claims;
using System.Security.Cryptography;
using JOrder.Identity.Models;
using JOrder.Identity.Options;
using JOrder.Identity.Services;
using JOrder.Identity.Services.Interfaces;
using JOrder.Testing.Time;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Services;

public class TokenMintingServiceUnitTests
{
    [Fact]
    public void MintAccessToken_ReturnsTokenWithExpectedClaims()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa);
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var signingKeyMaterialService = Substitute.For<ISigningKeyMaterialService>();
        signingKeyMaterialService.GetSigningCredentials().Returns(signingCredentials);

        var options = Microsoft.Extensions.Options.Options.Create(new JwtSigningOptions
        {
            Issuer = "https://issuer.example.com",
            Audience = "jorder-api",
            AccessTokenLifetimeMinutes = 15
        });

        var now = new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero);
        var service = new TokenMintingService(signingKeyMaterialService, options, new FixedTimeProvider(now));

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "john@example.com",
            UserName = "john@example.com"
        };

        // Act
        var (token, expiresAt) = service.MintAccessToken(user, ["Customer", "Admin"]);

        // Assert
        Assert.Equal(now.AddMinutes(15), expiresAt);

        var jwt = new JsonWebToken(token);
        Assert.Equal("https://issuer.example.com", jwt.Issuer);
        Assert.Contains("jorder-api", jwt.Audiences);
        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal("john@example.com", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);

        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();
        Assert.Equal(["Customer", "Admin"], roles);

        signingKeyMaterialService.Received(1).GetSigningCredentials();
    }

    [Fact]
    public void MintRefreshToken_ReturnsHashMatchingRawTokenAndConfiguredExpiry()
    {
        // Arrange
        var signingKeyMaterialService = Substitute.For<ISigningKeyMaterialService>();
        var now = new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero);

        var options = Microsoft.Extensions.Options.Options.Create(new JwtSigningOptions
        {
            RefreshTokenLifetimeDays = 10
        });

        var service = new TokenMintingService(signingKeyMaterialService, options, new FixedTimeProvider(now));

        // Act
        var (rawToken, tokenHash, expiresAt) = service.MintRefreshToken();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(rawToken));
        Assert.False(string.IsNullOrWhiteSpace(tokenHash));
        Assert.Equal(now.AddDays(10), expiresAt);
        Assert.Equal(service.HashToken(rawToken), tokenHash);
    }

    [Fact]
    public void HashToken_SameInput_ReturnsSameHash()
    {
        // Arrange
        var service = new TokenMintingService(
            Substitute.For<ISigningKeyMaterialService>(),
            Microsoft.Extensions.Options.Options.Create(new JwtSigningOptions()),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        // Act
        var hashA = service.HashToken("sample-token");
        var hashB = service.HashToken("sample-token");

        // Assert
        Assert.Equal(hashA, hashB);
    }
}




