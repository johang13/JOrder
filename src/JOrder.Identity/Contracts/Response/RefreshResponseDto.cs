namespace JOrder.Identity.Contracts.Response;

public sealed record RefreshResponseDto
{
    public required string AccessToken { get; init; }
    public DateTimeOffset AccessTokenExpiresAt { get; init; }
    public required string RefreshToken { get; init; }
    public DateTimeOffset RefreshTokenExpiresAt { get; init; }
}

