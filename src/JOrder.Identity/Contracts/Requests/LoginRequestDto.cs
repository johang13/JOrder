using System.ComponentModel.DataAnnotations;

namespace JOrder.Identity.Contracts.Requests;

public sealed record LoginRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
    [Required]
    public string Password { get; init; } = string.Empty;
}