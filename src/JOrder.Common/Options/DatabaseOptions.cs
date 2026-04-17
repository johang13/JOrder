using System.ComponentModel.DataAnnotations;
using JOrder.Common.Options.Interfaces;

namespace JOrder.Common.Options;

public sealed class DatabaseOptions : IJOrderOptions
{
    public static string SectionName => "JOrder:DatabaseOptions";

    [Required]
    public string Provider { get; init; } = string.Empty;

    [Required]
    public string ConnectionString { get; init; } = string.Empty;
}