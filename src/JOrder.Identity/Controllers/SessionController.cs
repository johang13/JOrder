using JOrder.Common.Extensions;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JOrder.Identity.Controllers;

[ApiController]
[Route("[controller]")]
public class SessionController(ISessionService sessionService) : ControllerBase
{
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LogoutAll()
    {
        var userIdClaim = this.GetUserIdClaim();
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var command = new LogoutAllCommand(userId);
        var result = await sessionService.LogoutAllAsync(command, HttpContext.RequestAborted);
        
        if (result.IsFailure)
            return this.ToActionResult(result.Error);
        
        return NoContent();
    }
}
