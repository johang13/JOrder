using Microsoft.IdentityModel.Tokens;

namespace JOrder.Identity.Contracts.Responses;

public sealed record JsonWebKeySetResponseDto
{
    public required IReadOnlyCollection<JsonWebKey> Keys { get; init; }
}