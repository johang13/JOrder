namespace JOrder.Identity.Contracts.Response;

public sealed record UserProfileDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; }  = string.Empty;
    public string Email { get; init; }  = string.Empty;
    public string[] Roles { get; init; } = [];
}