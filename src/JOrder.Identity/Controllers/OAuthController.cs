using JOrder.Common.Abstractions.Results;
using JOrder.Common.Attributes;
using JOrder.Common.Extensions;
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
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status409Conflict)]
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
    /// OAuth 2.0 authorization endpoint. Initiates the Authorization Code flow.
    /// </summary>
    /// <remarks>
    /// This endpoint supports the OAuth2 Authorization Code flow for interactive login.
    /// If the user is not authenticated, they will be redirected to a login page.
    /// Upon successful authentication and consent, an authorization code is issued.
    /// </remarks>
    /// <response code="302">Redirect to login or back to redirect_uri with authorization code.</response>
    /// <response code="400">Invalid request parameters.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("authorize")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public IActionResult Authorize(
        [FromQuery] string? client_id,
        [FromQuery] string? redirect_uri,
        [FromQuery] string? response_type,
        [FromQuery] string? scope,
        [FromQuery] string? state)
    {
        // Validate required parameters
        if (string.IsNullOrWhiteSpace(response_type) || response_type != "code")
            return OAuthError("invalid_request", "response_type must be 'code'.", StatusCodes.Status400BadRequest);

        if (string.IsNullOrWhiteSpace(redirect_uri))
            return OAuthError("invalid_request", "redirect_uri is required.", StatusCodes.Status400BadRequest);

        // For documentation tools (Scalar, Swagger UI), we can allow implicit authorization
        // In production, you'd validate client_id against registered applications
        if (string.IsNullOrWhiteSpace(client_id))
            client_id = "swagger";

        // If user is authenticated, immediately issue authorization code and redirect
        var userIdClaim = this.GetUserIdClaim();
        if (Guid.TryParse(userIdClaim, out _))
        {
            var authorizationCode = GenerateAuthorizationCode();
            var redirectUrl = BuildRedirectUrl(redirect_uri, authorizationCode, state);
            return Redirect(redirectUrl);
        }

        // User not authenticated - for API docs, redirect to a simple login endpoint
        var loginRedirect = $"/oauth2/login?redirect_uri={Uri.EscapeDataString(redirect_uri)}&client_id={Uri.EscapeDataString(client_id)}&scope={Uri.EscapeDataString(scope ?? "")}&state={Uri.EscapeDataString(state ?? "")}";
        return Redirect(loginRedirect);
    }

    /// <summary>
    /// Simple login page for Authorization Code flow.
    /// </summary>
    /// <remarks>
    /// Provides a basic login form for users to authenticate.
    /// After successful login, redirects back to the authorization endpoint.
    /// </remarks>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(
        [FromQuery] string? redirect_uri,
        [FromQuery] string? client_id,
        [FromQuery] string? scope,
        [FromQuery] string? state)
    {
        // In a real application, this would render a login form
        // For now, return a simple message directing users to the token endpoint
        var response = new
        {
            message = "For interactive login, please use the password grant at /oauth2/token",
            instructions = new
            {
                method = "POST",
                endpoint = "/oauth2/token",
                contentType = "application/x-www-form-urlencoded",
                parameters = new
                {
                    grant_type = "password",
                    username = "your_email@example.com",
                    password = "your_password"
                }
            },
            redirect_uri,
            scope
        };

        return Ok(response);
    }

    private static string GenerateAuthorizationCode()
    {
        // Generate a random authorization code (in production, store this with expiration)
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string BuildRedirectUrl(string redirectUri, string code, string? state)
    {
        var separator = redirectUri.Contains('?') ? '&' : '?';
        var url = $"{redirectUri}{separator}code={Uri.EscapeDataString(code)}";

        if (!string.IsNullOrWhiteSpace(state))
            url += $"&state={Uri.EscapeDataString(state)}";

        return url;
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
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(OAuthErrorResponseDto), StatusCodes.Status409Conflict)]
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
        return error.Type switch
        {
            ErrorType.Validation => OAuthError("invalid_request", error.Description, StatusCodes.Status400BadRequest),
            ErrorType.Unauthorized => OAuthError("invalid_grant", error.Description, StatusCodes.Status401Unauthorized),
            ErrorType.Forbidden => OAuthError("invalid_scope", error.Description, StatusCodes.Status403Forbidden),
            ErrorType.NotFound => OAuthError("invalid_request", error.Description, StatusCodes.Status404NotFound),
            ErrorType.Conflict => OAuthError("invalid_request", error.Description, StatusCodes.Status409Conflict),
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
