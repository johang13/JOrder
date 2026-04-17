using System.ComponentModel.DataAnnotations;

namespace JOrder.Identity.Contracts.Requests;

public sealed record RegisterRequestDto
{
    [Required]
    public string FirstName { get; init; } = string.Empty;
    [Required]
    public string LastName { get; init; } = string.Empty;
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
    [Required]
    public string Password { get; init; } = string.Empty;
}