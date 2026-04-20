namespace JOrder.Identity.Application.Users.Results;

public sealed record UserProfileResult(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string[] Roles,
    bool IsActive);