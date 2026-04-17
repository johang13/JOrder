using System.ComponentModel.DataAnnotations;

namespace JOrder.Identity.Contracts.Requests;

public sealed record ChangePasswordRequestDto
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;
    [Required]
    public string NewPassword { get; init; } = string.Empty;
}