using System.ComponentModel.DataAnnotations;
using JOrder.Common.Options.Interfaces;

namespace JOrder.Common.Options;

public sealed class ServiceOptions : IJOrderOptions
{
    public static string SectionName => "JOrder:ServiceOptions";

    [Required]
    public string Name { get; init; } = string.Empty;
    [Required]
    public string Version { get; init; } = string.Empty;
}