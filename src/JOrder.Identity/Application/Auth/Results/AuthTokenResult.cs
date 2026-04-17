namespace JOrder.Identity.Application.Auth.Results;

public sealed record AuthTokenResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

