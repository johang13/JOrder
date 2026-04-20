namespace JOrder.Identity.Contracts.Responses;

public sealed record RefreshResponseDto
{
    public required string AccessToken { get; init; }
    public DateTimeOffset AccessTokenExpiresAt { get; init; }
    public required string RefreshToken { get; init; }
    public DateTimeOffset RefreshTokenExpiresAt { get; init; }
}

