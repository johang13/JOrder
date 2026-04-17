using System.ComponentModel.DataAnnotations;

namespace JOrder.Identity.Contracts.Requests;

public sealed record RefreshRequestDto
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

