using System.Text.Json.Serialization;

namespace JOrder.Identity.Contracts.Responses;

public sealed record OAuthErrorResponseDto
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    [JsonPropertyName("error_description")]
    public required string ErrorDescription { get; init; }
}
