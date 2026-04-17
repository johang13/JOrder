namespace JOrder.Identity.Application.Auth.Results;

public record UserProfileResult(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string[] Roles,
    bool IsActive);