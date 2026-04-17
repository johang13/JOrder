using System.Security.Claims;
using JOrder.Common.Extensions;
using JOrder.Common.Attributes;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Contracts.Requests;
using JOrder.Identity.Contracts.Response;
using JOrder.Identity.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace JOrder.Identity.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// Registers a new user account and issues an access token and refresh token.
    /// </summary>
    /// <response code="201">Registration succeeded and tokens were issued.</response>
    /// <response code="400">The request payload is invalid.</response>
    /// <response code="409">A user with the same email already exists.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [RateLimit(permitLimit: 10, windowSeconds: 60)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(RegisterResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RegisterResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        var command = new RegisterCommand(
            request.FirstName, request.LastName, request.Email, request.Password,
            GetIpAddress(), GetUserAgent());

        var result = await authService.RegisterAsync(command, HttpContext.RequestAborted);

        if (result.IsFailure)
            return this.ToActionResult(result.Error);

        return StatusCode(StatusCodes.Status201Created, new RegisterResponseDto
        {
            AccessToken = result.Value.AccessToken,
            AccessTokenExpiresAt = result.Value.AccessTokenExpiresAt,
            RefreshToken = result.Value.RefreshToken,
            RefreshTokenExpiresAt = result.Value.RefreshTokenExpiresAt,
        });
    }

    /// <summary>
    /// Authenticates a user and issues an access token and refresh token.
    /// </summary>
    /// <response code="200">Authentication succeeded and tokens were issued.</response>
    /// <response code="401">Credentials are invalid.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [RateLimit(permitLimit: 10, windowSeconds: 60)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var command = new LoginCommand(request.Email, request.Password, GetIpAddress(), GetUserAgent());
        var result = await authService.LoginAsync(command, HttpContext.RequestAborted);

        if (result.IsFailure)
            return this.ToActionResult(result.Error);

        return Ok(new LoginResponseDto
        {
            AccessToken = result.Value.AccessToken,
            AccessTokenExpiresAt = result.Value.AccessTokenExpiresAt,
            RefreshToken = result.Value.RefreshToken,
            RefreshTokenExpiresAt = result.Value.RefreshTokenExpiresAt,
        });
    }

    /// <summary>
    /// Rotates the provided refresh token and returns a new token pair.
    /// </summary>
    /// <response code="200">Refresh succeeded and a new token pair was issued.</response>
    /// <response code="401">Refresh token is invalid, expired, revoked, or otherwise unauthorized.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [RateLimit(permitLimit: 10, windowSeconds: 60)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(RefreshResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RefreshResponseDto>> Refresh([FromBody] RefreshRequestDto request)
    {
        var command = new RefreshCommand(request.RefreshToken, GetIpAddress(), GetUserAgent());
        var result = await authService.RefreshAsync(command, HttpContext.RequestAborted);

        if (result.IsFailure)
            return this.ToActionResult(result.Error);

        return Ok(new RefreshResponseDto
        {
            AccessToken = result.Value.AccessToken,
            AccessTokenExpiresAt = result.Value.AccessTokenExpiresAt,
            RefreshToken = result.Value.RefreshToken,
            RefreshTokenExpiresAt = result.Value.RefreshTokenExpiresAt,
        });
    }

    /// <summary>
    /// Revokes the provided refresh token.
    /// </summary>
    /// <remarks>
    /// This endpoint is idempotent. It returns <c>204</c> whether the token was active,
    /// already revoked, expired, or not found.
    /// </remarks>
    /// <response code="204">Logout completed and token is no longer usable.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("logout")]
    [AllowAnonymous]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request)
    {
        var command = new LogoutCommand(request.RefreshToken);
        var result = await authService.LogoutAsync(command, HttpContext.RequestAborted);
        
        if (result.IsFailure)
            return this.ToActionResult(result.Error);
        
        return NoContent();
    }
    
    /// <summary>
    /// Revokes all active refresh tokens for the currently authenticated user.
    /// </summary>
    /// <remarks>
    /// Only active tokens are revoked. Already revoked or expired tokens are left unchanged.
    /// </remarks>
    /// <response code="204">All active sessions have been logged out.</response>
    /// <response code="401">The caller is not authenticated or user claims are invalid.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("logout-all")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LogoutAll()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var command = new LogoutAllCommand(userId);
        var result = await authService.LogoutAllAsync(command, HttpContext.RequestAborted);
        
        if (result.IsFailure)
            return this.ToActionResult(result.Error);
        
        return NoContent();
    }

    #region Helpers

    private string GetIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private string GetUserAgent() =>
        Request.Headers.UserAgent.ToString();

    #endregion
}