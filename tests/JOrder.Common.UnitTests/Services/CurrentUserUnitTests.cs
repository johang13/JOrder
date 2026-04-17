using System.Security.Claims;
using JOrder.Common.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;

namespace JOrder.Common.UnitTests.Services;

public class CurrentUserUnitTests
{
    private static IHttpContextAccessor BuildAccessor(HttpContext? httpContext)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return accessor;
    }

    private static HttpContext BuildContext(ClaimsPrincipal user)
    {
        return new DefaultHttpContext
        {
            User = user
        };
    }
    
    [Fact]
    public void Constructor_NoHttpContext_UserIsAnonymous()
    {
        var accessor = BuildAccessor(httpContext: null);

        var sut = new CurrentUser(accessor);

        Assert.False(sut.IsAuthenticated);
        Assert.Null(sut.Id);
        Assert.Null(sut.Email);
    }

    [Fact]
    public void Constructor_UnauthenticatedIdentity_UserIsAnonymous()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var accessor = BuildAccessor(BuildContext(principal));

        var sut = new CurrentUser(accessor);

        Assert.False(sut.IsAuthenticated);
        Assert.Null(sut.Id);
        Assert.Null(sut.Email);
    }

    [Fact]
    public void Constructor_AuthenticatedIdentityWithValidClaims_SetsUserData()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Email, "chris@example.com")
        ], "TestAuthType"));

        var accessor = BuildAccessor(BuildContext(principal));

        var sut = new CurrentUser(accessor);

        Assert.True(sut.IsAuthenticated);
        Assert.Equal(userId, sut.Id);
        Assert.Equal("chris@example.com", sut.Email);
    }

    [Fact]
    public void Constructor_AuthenticatedIdentityWithInvalidSub_LeavesIdNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, "not-a-guid"),
            new Claim(ClaimTypes.Email, "chris@example.com")
        ], "TestAuthType"));

        var accessor = BuildAccessor(BuildContext(principal));

        var sut = new CurrentUser(accessor);

        Assert.True(sut.IsAuthenticated);
        Assert.Null(sut.Id);
        Assert.Equal("chris@example.com", sut.Email);
    }

    [Fact]
    public void Constructor_AuthenticatedIdentityWithoutEmail_EmailIsNull()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())
        ], "TestAuthType"));

        var accessor = BuildAccessor(BuildContext(principal));

        var sut = new CurrentUser(accessor);

        Assert.True(sut.IsAuthenticated);
        Assert.Equal(userId, sut.Id);
        Assert.Null(sut.Email);
    }

    [Fact]
    public void Constructor_AuthenticatedIdentityWithoutSub_IdIsNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "chris@example.com")
        ], "TestAuthType"));

        var accessor = BuildAccessor(BuildContext(principal));

        var sut = new CurrentUser(accessor);

        Assert.True(sut.IsAuthenticated);
        Assert.Null(sut.Id);
        Assert.Equal("chris@example.com", sut.Email);
    }
}