using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JOrder.Common.Attributes;
using JOrder.Identity.Models;
using JOrder.Identity.Options;
using JOrder.Identity.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JOrder.Identity.Services;

[SingletonService]
public sealed class TokenMintingService(
    ISigningKeyMaterialService signingKeyMaterialService,
    IOptions<JwtSigningOptions> signingOptions,
    TimeProvider timeProvider) : ITokenMintingService
{
    public (string Token, DateTimeOffset ExpiresAt) MintAccessToken(User user, IReadOnlyCollection<string> roles)
    {
        var options = signingOptions.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(options.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email ?? user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = signingKeyMaterialService.GetSigningCredentials()
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(descriptor);

        return (token, expiresAt);
    }

    public (string RawToken, string TokenHash, DateTimeOffset ExpiresAt) MintRefreshToken()
    {
        var options = signingOptions.Value;
        var expiresAt = timeProvider.GetUtcNow().AddDays(options.RefreshTokenLifetimeDays);

        // 64 bytes = 512 bits of entropy
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(randomBytes);
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        return (rawToken, tokenHash, expiresAt);
    }

    public string HashToken(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
