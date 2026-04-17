using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using JOrder.Identity.Contracts.Response;
using JOrder.Identity.Options;
using JOrder.Identity.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JOrder.Identity.Controllers;

[ApiController]
[Route(".well-known")]
[AllowAnonymous]
public class WellKnownController(
    IOptions<JwtSigningOptions> signingOptionsAccessor,
    ISigningKeyMaterialService signingKeyMaterialService,
    ILogger<WellKnownController> logger) : ControllerBase
{
    [HttpGet("openid-configuration")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<OpenIdConfigurationResponseDto> GetOpenIdConfiguration()
    {
        logger.LogInformation("Getting openid configuration");
        
        var signingOptions = signingOptionsAccessor.Value;

        var issuer = signingOptions.Issuer.TrimEnd('/');

        var response = new OpenIdConfigurationResponseDto
        {
            Issuer = signingOptions.Issuer,
            JwksUri = $"{issuer}/.well-known/jwks.json",
            IdTokenSigningAlgValuesSupported = [signingOptions.Algorithm],
            ClaimsSupported =
            [
                JwtRegisteredClaimNames.Sub,
                JwtRegisteredClaimNames.Email,
                ClaimTypes.Role,
            ]
        };

        return Ok(response);
    }
    
    [HttpGet("jwks.json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<JsonWebKeySetResponseDto> GetJsonWebKeySet()
    {
        logger.LogInformation("Getting jwk set");
        
        return Ok(new JsonWebKeySetResponseDto
        {
            Keys = [signingKeyMaterialService.GetPublicJsonWebKey()]
        });
    }
}