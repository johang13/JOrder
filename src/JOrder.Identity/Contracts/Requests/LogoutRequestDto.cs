using System.ComponentModel.DataAnnotations;

namespace JOrder.Identity.Contracts.Requests;

public sealed record LogoutRequestDto
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}