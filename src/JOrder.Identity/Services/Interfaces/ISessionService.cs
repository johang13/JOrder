using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Application.Auth.Commands;

namespace JOrder.Identity.Services.Interfaces;

public interface ISessionService
{
    Task<Result> LogoutAllAsync(LogoutAllCommand command, CancellationToken cancellationToken = default);
}
