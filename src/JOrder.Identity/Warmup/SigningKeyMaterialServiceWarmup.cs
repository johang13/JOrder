using JOrder.Common.Services.Interfaces;
using JOrder.Identity.Services.Interfaces;

namespace JOrder.Identity.Warmup;

public class SigningKeyMaterialServiceWarmup(ISigningKeyMaterialService signingKeyMaterialService) : IJOrderWarmupTask
{
    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ = signingKeyMaterialService.GetSigningCredentials();
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }
}