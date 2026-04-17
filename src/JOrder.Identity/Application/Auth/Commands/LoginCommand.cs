namespace JOrder.Identity.Application.Auth.Commands;

public sealed record LoginCommand(string Email, string Password, string IpAddress, string UserAgent);

