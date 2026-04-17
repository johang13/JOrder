using System.ComponentModel.DataAnnotations;
using JOrder.Common.Options.Interfaces;

namespace JOrder.Common.Options;

public sealed class JwtValidationOptions : IJOrderOptions
{
    public static string SectionName => "JOrder:Authentication:JwtValidation";

    [Required]
    [Url]
    public string Authority { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; } = true;
}