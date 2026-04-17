using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Application.Auth.Results;

namespace JOrder.Identity.Services.Interfaces;

public interface IAuthService
{
    Task<Result<AuthTokenResult>> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken = default);
    Task<Result<AuthTokenResult>> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default);
    Task<Result<AuthTokenResult>> RefreshAsync(RefreshCommand command, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(LogoutCommand command, CancellationToken cancellationToken = default);
    Task<Result> LogoutAllAsync(LogoutAllCommand command, CancellationToken cancellationToken = default);
}