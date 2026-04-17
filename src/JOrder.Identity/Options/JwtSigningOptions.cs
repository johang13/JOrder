using System.ComponentModel.DataAnnotations;
using JOrder.Common.Options.Interfaces;

namespace JOrder.Identity.Options;

public sealed class JwtSigningOptions : IJOrderOptions
{
    public static string SectionName => "Authentication:JwtSigning";

    [Required]
    public string PrivateKeyPath { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    public string Algorithm { get; set; } = "RS256";

    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    public int RefreshTokenLifetimeDays { get; set; } = 7;
}