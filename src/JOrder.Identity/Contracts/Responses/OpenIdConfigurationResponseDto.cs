using System.Text.Json.Serialization;

namespace JOrder.Identity.Contracts.Responses;

public sealed record OpenIdConfigurationResponseDto
{
    [JsonPropertyName("issuer")]
    public string Issuer { get; init; } = string.Empty;

    [JsonPropertyName("jwks_uri")]
    public string JwksUri { get; init; } = string.Empty;

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; init; } = string.Empty;

    [JsonPropertyName("userinfo_endpoint")]
    public string UserInfoEndpoint { get; init; } = string.Empty;

    [JsonPropertyName("revocation_endpoint")]
    public string RevocationEndpoint { get; init; } = string.Empty;

    [JsonPropertyName("end_session_endpoint")]
    public string EndSessionEndpoint { get; init; } = string.Empty;

    [JsonPropertyName("subject_types_supported")]
    public string[] SubjectTypesSupported { get; init; } = ["public"];

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public string[] IdTokenSigningAlgValuesSupported { get; init; } = [];

    [JsonPropertyName("grant_types_supported")]
    public string[] GrantTypesSupported { get; init; } = [];

    [JsonPropertyName("response_types_supported")]
    public string[] ResponseTypesSupported { get; init; } = [];

    [JsonPropertyName("scopes_supported")]
    public string[] ScopesSupported { get; init; } = [];

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public string[] TokenEndpointAuthMethodsSupported { get; init; } = [];

    [JsonPropertyName("claims_supported")]
    public string[] ClaimsSupported { get; init; } = [];
}