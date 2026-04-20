using JOrder.Identity.Contracts.Responses;
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
    /// <summary>
    /// Returns the OpenID Connect discovery document for this identity server.
    /// </summary>
    /// <response code="200">The discovery document was returned successfully.</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpGet("openid-configuration")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<OpenIdConfigurationResponseDto> GetOpenIdConfiguration()
    {
        logger.LogDebug("Getting openid configuration");
        
        var signingOptions = signingOptionsAccessor.Value;

        var issuer = signingOptions.Issuer.TrimEnd('/');

        var response = new OpenIdConfigurationResponseDto
        {
            Issuer = signingOptions.Issuer,
            JwksUri = $"{issuer}/.well-known/jwks.json",
            TokenEndpoint = $"{issuer}/oauth2/token",
            RevocationEndpoint = $"{issuer}/oauth2/revoke",
            EndSessionEndpoint = $"{issuer}/Session/logout-all",
            GrantTypesSupported = ["password", "refresh_token"],
            TokenEndpointAuthMethodsSupported = ["none"],
        };

        return Ok(response);
    }
    
    /// <summary>
    /// Returns the JSON Web Key Set used to validate issued JWT signatures.
    /// </summary>
    /// <response code="200">The JWK set was returned successfully.</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpGet("jwks.json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<JsonWebKeySetResponseDto> GetJsonWebKeySet()
    {
        logger.LogDebug("Getting jwk set");
        
        return Ok(new JsonWebKeySetResponseDto
        {
            Keys = [signingKeyMaterialService.GetPublicJsonWebKey()]
        });
    }
}