using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Application.Auth.Commands;
using JOrder.Identity.Controllers;
using JOrder.Identity.Services.Interfaces;
using JOrder.Testing.Controllers;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Controllers;

public class SessionControllerUnitTests : ApiControllerUnitTestBase
{
    private readonly ISessionService _sessionService;
    private readonly SessionController _sessionController;
    
    public SessionControllerUnitTests()
    {
        _sessionService = Substitute.For<ISessionService>();
        _sessionController = new SessionController(_sessionService);
        
        AttachHttpContext(_sessionController, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");
    }

    [Fact]
    public async Task LogoutAll_Success_WithAuthenticatedUser_ReturnsNoContent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        AttachAuthenticatedHttpContext(_sessionController, userId, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");

        _sessionService.LogoutAllAsync(Arg.Any<LogoutAllCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _sessionController.LogoutAll();

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _sessionService.Received(1).LogoutAllAsync(
            Arg.Is<LogoutAllCommand>(c => c.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAll_InvalidUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        AttachHttpContext(_sessionController, userAgent: "JOrder.UnitTests/1.0", remoteIp: "127.0.0.1");
        // No authenticated user attached, so GetUserIdClaim returns null

        // Act
        var result = await _sessionController.LogoutAll();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        await _sessionService.DidNotReceive().LogoutAllAsync(Arg.Any<LogoutAllCommand>(), Arg.Any<CancellationToken>());
    }
}
