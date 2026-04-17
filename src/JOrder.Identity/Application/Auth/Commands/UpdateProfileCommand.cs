namespace JOrder.Identity.Application.Auth.Commands;

public sealed record UpdateProfileCommand(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? Email);

