using JOrder.Common.Attributes;
using JOrder.Common.Extensions;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Application.Users.Commands;
using JOrder.Identity.Contracts.Requests;
using JOrder.Identity.Contracts.Responses;
using JOrder.Identity.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JOrder.Identity.Controllers;

[ApiController]
[Route("[controller]")]
public class UsersController(IUsersService usersService) : ControllerBase
{
    /// <summary>
    /// Registers a new user account.
    /// </summary>
    /// <response code="201">Registration succeeded.</response>
    /// <response code="400">The request payload is invalid.</response>
    /// <response code="409">A user with the same email already exists.</response>
    /// <response code="429">Too many requests in a short time window.</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpPost]
    [AllowAnonymous]
    [RateLimit(permitLimit: 10, windowSeconds: 60)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var command = new RegisterCommand(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            GetIpAddress(),
            GetUserAgent());

        var result = await usersService.RegisterAsync(command, HttpContext.RequestAborted);

        if (result.IsFailure)
            return this.ToActionResult(result.Error);

        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Gets the profile of the currently authenticated user.
    /// </summary>
    /// <response code="200">The user profile was returned successfully.</response>
    /// <response code="400">The request is invalid.</response>
    /// <response code="401">The caller is unauthenticated or user claims are invalid.</response>
    /// <response code="404">The user or associated roles were not found.</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserProfileDto>> GetUserProfile()
    {
        var userIdClaim = this.GetUserIdClaim();
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var command = new UserProfileCommand(userId);
        var result = await usersService.GetUserProfileAsync(command, HttpContext.RequestAborted);

        if (result.IsFailure)
            return this.ToActionResult(result.Error);

        var profile = result.Value;
        var response = new UserProfileDto
        {
            Id = profile.Id,
            Email = profile.Email,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Roles = profile.Roles
        };

        return Ok(response);
    }
    
    /// <summary>
    /// Updates the profile of the currently authenticated user.
    /// Only the fields provided in the request body are updated; omitted fields are left unchanged.
    /// </summary>
    /// <response code="200">The updated user profile.</response>
    /// <response code="400">The request is invalid or a field update failed validation.</response>
    /// <response code="401">The caller is unauthenticated or user claims are invalid.</response>
    /// <response code="404">The user was not found.</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpPatch("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile([FromBody] UpdateProfileRequestDto request)
    {
        var userIdClaim = this.GetUserIdClaim();
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var command = new UpdateProfileCommand(userId, request.FirstName, request.LastName, request.Email);
        var result = await usersService.UpdateProfileAsync(command, HttpContext.RequestAborted);

        if (result.IsFailure)
            return this.ToActionResult(result.Error);

        var profile = result.Value;
        return Ok(new UserProfileDto
        {
            Id = profile.Id,
            Email = profile.Email,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Roles = profile.Roles
        });
    }
    
    /// <summary>
    /// Changes the password for the currently authenticated user.
    /// </summary>
    /// <response code="204">Password was changed successfully.</response>
    /// <response code="400">The request is invalid or the password change failed validation.</response>
    /// <response code="401">The caller is unauthenticated or user claims are invalid.</response>
    /// <response code="404">The user was not found.</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpPost("me/change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequestDto changePasswordRequestDto)
    {
        var userIdClaim = this.GetUserIdClaim();
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        
        var command = new ChangePasswordCommand(
            userId,
            changePasswordRequestDto.CurrentPassword,
            changePasswordRequestDto.NewPassword);
        
        var result = await usersService.ChangePasswordAsync(command, HttpContext.RequestAborted);
        
        if (result.IsFailure)
            return this.ToActionResult(result.Error);
        
        return NoContent();
    }

    private string GetIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private string GetUserAgent() =>
        Request.Headers.UserAgent.ToString();
}