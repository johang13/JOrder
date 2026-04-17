using JOrder.Common.Abstractions.Results;
using JOrder.Common.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JOrder.Common.UnitTests.Extensions;

public class ControllerBaseExtensionsUnitTests
{
    private readonly TestController _controller = new()
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public void ToActionResult_Validation_ReturnsBadRequest()
    {
        var result = _controller.ToActionResult(Error.Validation("validation", "Invalid"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid", badRequest.Value);
    }

    [Fact]
    public void ToActionResult_Conflict_ReturnsConflict()
    {
        var result = _controller.ToActionResult(Error.Conflict("conflict", "Duplicate"));

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("Duplicate", conflict.Value);
    }

    [Fact]
    public void ToActionResult_NotFound_ReturnsNotFound()
    {
        var result = _controller.ToActionResult(Error.NotFound("notfound", "Missing"));

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Missing", notFound.Value);
    }

    [Fact]
    public void ToActionResult_Unauthorized_ReturnsUnauthorized()
    {
        var result = _controller.ToActionResult(Error.Unauthorized("unauthorized", "No token"));

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("No token", unauthorized.Value);
    }

    [Fact]
    public void ToActionResult_Forbidden_Returns403ObjectResult()
    {
        var result = _controller.ToActionResult(Error.Forbidden("forbidden", "No access"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal("No access", objectResult.Value);
    }

    [Fact]
    public void ToActionResult_Failure_ReturnsProblem500()
    {
        var result = _controller.ToActionResult(Error.Failure("failure", "Unexpected"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);

        var details = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Unexpected", details.Detail);
    }

    [Fact]
    public void ToActionResult_None_FallsThrough_ReturnsProblem500()
    {
        var result = _controller.ToActionResult(Error.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    private sealed class TestController : ControllerBase;
}

