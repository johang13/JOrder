using JOrder.Identity.Models;

namespace JOrder.Identity.Services.Interfaces;

public interface ITokenMintingService
{
    (string Token, DateTimeOffset ExpiresAt) MintAccessToken(User user, IReadOnlyCollection<string> roles);
    (string RawToken, string TokenHash, DateTimeOffset ExpiresAt) MintRefreshToken();
    string HashToken(string rawToken);
}

