using System.ComponentModel.DataAnnotations;

namespace JOrder.Identity.Contracts.Requests;

public sealed record UpdateProfileRequestDto
{
    [MaxLength(100)]
    public string? FirstName { get; init; }

    [MaxLength(100)]
    public string? LastName { get; init; }

    [EmailAddress]
    [MaxLength(256)]
    public string? Email { get; init; }
}

