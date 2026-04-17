using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using JOrder.Common.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace JOrder.Common.Services;

public class CurrentUser : ICurrentUser
{
    public Guid? Id { get; }
    public string? Email { get; }
    public bool IsAuthenticated { get; }
    
    public CurrentUser(IHttpContextAccessor httpContextAccessor) 
    { 
        var user = httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true) return;
        IsAuthenticated = true;
        
        // User account: extract user details
        var userIdClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            Id = userId;
        }

        Email = user.FindFirst(ClaimTypes.Email)?.Value;
    }
}