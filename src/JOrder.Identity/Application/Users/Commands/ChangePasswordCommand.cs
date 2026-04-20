namespace JOrder.Identity.Application.Users.Commands;

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword);
