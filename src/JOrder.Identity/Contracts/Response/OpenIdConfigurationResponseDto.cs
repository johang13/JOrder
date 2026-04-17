namespace JOrder.Identity.Contracts.Response;

public sealed record OpenIdConfigurationResponseDto
{
    public string Issuer { get; init; } = string.Empty;
    public string JwksUri { get; init; } = string.Empty;
    public string[] SubjectTypesSupported { get; init; } = ["public"];
    public string[] IdTokenSigningAlgValuesSupported { get; init; } = [];
    public string[] ClaimsSupported { get; init; } = [];
}