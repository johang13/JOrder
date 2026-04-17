namespace JOrder.Identity.Application.Auth.Commands;

public sealed record RegisterCommand(string FirstName, string LastName, string Email, string Password, string IpAddress, string UserAgent);

