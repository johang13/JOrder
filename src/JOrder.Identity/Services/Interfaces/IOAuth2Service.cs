using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Application.Auth.Results;

namespace JOrder.Identity.Services.Interfaces;

public interface IOAuth2Service
{
    Task<Result<AuthTokenResult>> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default);
    Task<Result<AuthTokenResult>> RefreshAsync(RefreshCommand command, CancellationToken cancellationToken = default);
    Task<Result> RevokeAsync(LogoutCommand command, CancellationToken cancellationToken = default);
}
