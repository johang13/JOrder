using Microsoft.AspNetCore.Mvc;

namespace JOrder.Identity.Contracts.Requests;

public sealed record OAuthTokenRequestDto
{
    [FromForm(Name = "grant_type")]
    public string GrantType { get; init; } = string.Empty;

    [FromForm(Name = "username")]
    public string? Username { get; init; }

    [FromForm(Name = "password")]
    public string? Password { get; init; }

    [FromForm(Name = "refresh_token")]
    public string? RefreshToken { get; init; }

    [FromForm(Name = "scope")]
    public string? Scope { get; init; }
}
