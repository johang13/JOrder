using JOrder.Common.Abstractions.Results;
using JOrder.Common.Attributes;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Application.Auth.Results;
using JOrder.Identity.Contracts.Requests;
using JOrder.Identity.Contracts.Responses;
using JOrder.Identity.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JOrder.Identity.Controllers;

[ApiController]
[Route("oauth2")]
[AllowAnonymous]
public class OAuthController(IOAuth2Service oauth2Service) : ControllerBase
{
    /// <summary>
    /// OAuth 2.0 token endpoint. Supports <c>password</c> and <c>refresh_token</c> grants.
    /// </summary>
    /// <response code="200">Token request succeeded and tokens were issued.</response>
    /// <response code="400">The request is invalid or grant cannot be fulfilled.</response>
    /// <response code="429">Too many requests in a short time window.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("token")]
    [RateLimit(permitLimit: 10, windowSeconds: 60)]
    [Consumes("application/x-www-form-urlencoded")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(OAuthTokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Token([FromForm] OAuthTokenRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.GrantType))
            return OAuthError("invalid_request", "grant_type is required.");

        switch (request.GrantType.Trim().ToLowerInvariant())
        {
            case "password":
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                    return OAuthError("invalid_request", "username and password are required for the password grant.");

                var command = new LoginCommand(request.Username, request.Password, GetIpAddress(), GetUserAgent());
                var result = await oauth2Service.LoginAsync(command, HttpContext.RequestAborted);

                if (result.IsFailure)
                    return ToOAuthError(result.Error);

                return Ok(ToOAuthTokenResponse(result.Value));
            }
            case "refresh_token":
            {
                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                    return OAuthError("invalid_request", "refresh_token is required for the refresh_token grant.");

                var command = new RefreshCommand(request.RefreshToken, GetIpAddress(), GetUserAgent());
                var result = await oauth2Service.RefreshAsync(command, HttpContext.RequestAborted);

                if (result.IsFailure)
                    return ToOAuthError(result.Error);

                return Ok(ToOAuthTokenResponse(result.Value));
            }
            default:
                return OAuthError("unsupported_grant_type", "Only password and refresh_token grants are supported.");
        }
    }


    /// <summary>
    /// OAuth 2.0 revocation endpoint. Revokes the provided refresh token.
    /// </summary>
    /// <remarks>
    /// This endpoint is idempotent. It returns <c>200</c> whether the token was active,
    /// already revoked, expired, or not found.
    /// </remarks>
    /// <response code="200">Revocation request processed.</response>
    /// <response code="400">The request payload is invalid.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("revoke")]
    [Consumes("application/x-www-form-urlencoded")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Revoke([FromForm] OAuthRevocationRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return OAuthError("invalid_request", "token is required.");

        var result = await oauth2Service.RevokeAsync(new LogoutCommand(request.Token), HttpContext.RequestAborted);
        if (result.IsFailure)
            return ToOAuthError(result.Error);

        return Ok();
    }

    private string GetIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private string GetUserAgent() =>
        Request.Headers.UserAgent.ToString();

    private static OAuthTokenResponseDto ToOAuthTokenResponse(AuthTokenResult tokenResult)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresIn = (int)Math.Max(0, Math.Ceiling((tokenResult.AccessTokenExpiresAt - now).TotalSeconds));

        return new OAuthTokenResponseDto
        {
            AccessToken = tokenResult.AccessToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            RefreshToken = tokenResult.RefreshToken,
        };
    }

    private IActionResult ToOAuthError(Error error)
    {
        // Map our internal error types to appropriate OAuth 2.0 error responses.
        // RFC 6749 specifies to return 400 for these errors
        return error.Type switch
        {
            ErrorType.Validation => OAuthError("invalid_request", error.Description),
            ErrorType.Unauthorized => OAuthError("invalid_grant", error.Description),
            ErrorType.Forbidden => OAuthError("invalid_scope", error.Description),
            ErrorType.NotFound => OAuthError("invalid_request", error.Description),
            ErrorType.Conflict => OAuthError("invalid_request", error.Description),
            _ => OAuthError("server_error", error.Description, StatusCodes.Status500InternalServerError),
        };
    }

    private static IActionResult OAuthError(string error, string description, int statusCode = StatusCodes.Status400BadRequest)
    {
        return new ObjectResult(new OAuthErrorResponseDto
        {
            Error = error,
            ErrorDescription = description,
        })
        {
            StatusCode = statusCode
        };
    }
}
