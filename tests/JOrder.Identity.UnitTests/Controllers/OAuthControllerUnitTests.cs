using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Application.Auth.Results;
using JOrder.Identity.Contracts.Requests;
using JOrder.Identity.Contracts.Responses;
using JOrder.Identity.Controllers;
using JOrder.Identity.Services.Interfaces;
using JOrder.Testing.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Controllers;

public class OAuthControllerUnitTests : ApiControllerUnitTestBase
{
    private readonly IOAuth2Service _oauth2Service;
    private readonly OAuthController _oauthController;

    public OAuthControllerUnitTests()
    {
        _oauth2Service = Substitute.For<IOAuth2Service>();
        _oauthController = new OAuthController(_oauth2Service);

        AttachHttpContext(_oauthController, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");
    }

    [Fact]
    public async Task Token_PasswordGrant_Success_ReturnsOkWithOAuthTokens()
    {
        _oauth2Service.LoginAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokenResult>.Success(new AuthTokenResult(
                "access_token",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "refresh_token",
                DateTimeOffset.UtcNow.AddDays(7))));

        var request = new OAuthTokenRequestDto
        {
            GrantType = "password",
            Username = "john@example.com",
            Password = "Password1!"
        };

        var result = await _oauthController.Token(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseDto = Assert.IsType<OAuthTokenResponseDto>(okResult.Value);
        Assert.Equal("access_token", responseDto.AccessToken);
        Assert.Equal("Bearer", responseDto.TokenType);
        Assert.Equal("refresh_token", responseDto.RefreshToken);
        Assert.True(responseDto.ExpiresIn > 0);

        await _oauth2Service.Received(1).LoginAsync(
            Arg.Is<LoginCommand>(c => c.Email == "john@example.com" && c.IpAddress == "127.0.0.1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Token_PasswordGrant_InvalidCredentials_ReturnsInvalidGrant()
    {
        _oauth2Service.LoginAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.Unauthorized("invalid_credentials", "Email or password is incorrect"));

        var request = new OAuthTokenRequestDto
        {
            GrantType = "password",
            Username = "john@example.com",
            Password = "WrongPassword"
        };

        var result = await _oauthController.Token(request);

        var unauthorized = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        var error = Assert.IsType<OAuthErrorResponseDto>(unauthorized.Value);
        Assert.Equal("invalid_grant", error.Error);
    }

    [Fact]
    public async Task Token_RefreshTokenGrant_Success_ReturnsOkWithOAuthTokens()
    {
        _oauth2Service.RefreshAsync(Arg.Any<RefreshCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokenResult>.Success(new AuthTokenResult(
                "new_access_token",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "new_refresh_token",
                DateTimeOffset.UtcNow.AddDays(7))));

        var request = new OAuthTokenRequestDto
        {
            GrantType = "refresh_token",
            RefreshToken = "old_refresh_token"
        };

        var result = await _oauthController.Token(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseDto = Assert.IsType<OAuthTokenResponseDto>(okResult.Value);
        Assert.Equal("new_access_token", responseDto.AccessToken);
        Assert.Equal("Bearer", responseDto.TokenType);

        await _oauth2Service.Received(1).RefreshAsync(
            Arg.Is<RefreshCommand>(c => c.RefreshToken == "old_refresh_token" && c.IpAddress == "127.0.0.1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Token_UnsupportedGrantType_ReturnsUnsupportedGrantTypeError()
    {
        var request = new OAuthTokenRequestDto { GrantType = "client_credentials" };

        var result = await _oauthController.Token(request);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        var error = Assert.IsType<OAuthErrorResponseDto>(badRequest.Value);
        Assert.Equal("unsupported_grant_type", error.Error);
    }

    [Fact]
    public async Task Revoke_Success_ReturnsOk()
    {
        _oauth2Service.RevokeAsync(Arg.Any<LogoutCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var request = new OAuthRevocationRequestDto { Token = "refresh_token_to_revoke" };

        var result = await _oauthController.Revoke(request);

        Assert.IsType<OkResult>(result);
        await _oauth2Service.Received(1).RevokeAsync(
            Arg.Is<LogoutCommand>(c => c.RefreshToken == "refresh_token_to_revoke"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Revoke_WithoutToken_ReturnsInvalidRequest()
    {
        var request = new OAuthRevocationRequestDto { Token = string.Empty };

        var result = await _oauthController.Revoke(request);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        var error = Assert.IsType<OAuthErrorResponseDto>(badRequest.Value);
        Assert.Equal("invalid_request", error.Error);
        await _oauth2Service.DidNotReceive().RevokeAsync(Arg.Any<LogoutCommand>(), Arg.Any<CancellationToken>());
    }
}
