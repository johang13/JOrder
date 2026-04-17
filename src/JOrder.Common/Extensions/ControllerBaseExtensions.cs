using JOrder.Common.Abstractions.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JOrder.Common.Extensions;

public static class ControllerBaseExtensions
{
    public static ActionResult ToActionResult(this ControllerBase controller, Error error)
    {
        return error.Type switch
        {
            ErrorType.Validation => controller.BadRequest(error.Description),
            ErrorType.Conflict => controller.Conflict(error.Description),
            ErrorType.NotFound => controller.NotFound(error.Description),
            ErrorType.Unauthorized => controller.Unauthorized(error.Description),
            ErrorType.Forbidden => new ObjectResult(error.Description) { StatusCode = StatusCodes.Status403Forbidden },
            _ => controller.Problem(detail: error.Description, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}

