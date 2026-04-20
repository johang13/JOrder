using Microsoft.AspNetCore.Mvc;

namespace JOrder.Identity.Contracts.Requests;

public sealed record OAuthRevocationRequestDto
{
    [FromForm(Name = "token")]
    public string Token { get; init; } = string.Empty;

    [FromForm(Name = "token_type_hint")]
    public string? TokenTypeHint { get; init; }
}
