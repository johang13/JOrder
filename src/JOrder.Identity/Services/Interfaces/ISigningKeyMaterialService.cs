using Microsoft.IdentityModel.Tokens;

namespace JOrder.Identity.Services.Interfaces;

public interface ISigningKeyMaterialService : IDisposable
{
    RsaSecurityKey GetSigningKey();
    SigningCredentials GetSigningCredentials();
    JsonWebKey GetPublicJsonWebKey();
}