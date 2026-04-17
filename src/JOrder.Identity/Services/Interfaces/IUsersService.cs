using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Application.Auth.Results;

namespace JOrder.Identity.Services.Interfaces;

public interface IUsersService
{
    Task<Result<UserProfileResult>> GetUserProfileAsync(UserProfileCommand command,
        CancellationToken cancellationToken = default);
    
    Task<Result> ChangePasswordAsync(ChangePasswordCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<UserProfileResult>> UpdateProfileAsync(UpdateProfileCommand command,
        CancellationToken cancellationToken = default);
}