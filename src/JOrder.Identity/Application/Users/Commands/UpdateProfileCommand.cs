namespace JOrder.Identity.Application.Users.Commands;

public sealed record UpdateProfileCommand(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? Email);

