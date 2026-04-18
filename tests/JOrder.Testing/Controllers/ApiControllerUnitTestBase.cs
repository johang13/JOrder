using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JOrder.Testing.Controllers;

public abstract class ApiControllerUnitTestBase
{
    private const string SubjectClaimType = "sub";

    protected static DefaultHttpContext CreateHttpContext(
        string? userAgent = "JOrder.UnitTests/1.0",
        string? remoteIp = "127.0.0.1",
        IEnumerable<Claim>? claims = null)
    {
        var httpContext = new DefaultHttpContext();

        if (userAgent is not null)
            httpContext.Request.Headers.UserAgent = userAgent;

        if (remoteIp is not null)
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);

        if (claims is not null)
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "UnitTest"));

        return httpContext;
    }

    protected static void AttachHttpContext(ControllerBase controller, DefaultHttpContext httpContext)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    protected static void AttachHttpContext(
        ControllerBase controller,
        string? userAgent = "JOrder.UnitTests/1.0",
        string? remoteIp = "127.0.0.1",
        IEnumerable<Claim>? claims = null)
    {
        var httpContext = CreateHttpContext(userAgent, remoteIp, claims);
        AttachHttpContext(controller, httpContext);
    }

    protected static IEnumerable<Claim> WithUserId(Guid userId, IEnumerable<Claim>? claims = null)
    {
        var filteredClaims = (claims ?? Enumerable.Empty<Claim>())
            .Where(c => c.Type != SubjectClaimType && c.Type != ClaimTypes.NameIdentifier)
            .ToList();

        filteredClaims.Add(new Claim(SubjectClaimType, userId.ToString()));
        return filteredClaims;
    }

    protected static void AttachAuthenticatedHttpContext(
        ControllerBase controller,
        Guid userId,
        string? userAgent = "JOrder.UnitTests/1.0",
        string? remoteIp = "127.0.0.1",
        IEnumerable<Claim>? claims = null)
    {
        AttachHttpContext(controller, userAgent, remoteIp, WithUserId(userId, claims));
    }
}

