namespace JOrder.Identity.Application.Auth.Commands;

public sealed record RefreshCommand(string RefreshToken, string IpAddress, string UserAgent);

