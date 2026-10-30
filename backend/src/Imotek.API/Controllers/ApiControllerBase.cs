using System.Text.Json;
using Imotek.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Imotek.API.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The only place where application errors become HTTP status codes.</summary>
    protected ActionResult ToProblem(Error error)
    {
        if (error.Type == ErrorType.Validation)
        {
            // camelCase keys, matching the JSON property names the client sent.
            var errors = (error.ValidationErrors ?? new Dictionary<string, string[]>())
                .ToDictionary(e => JsonNamingPolicy.CamelCase.ConvertName(e.Key), e => e.Value);
            return ValidationProblem(new ValidationProblemDetails(errors) { Title = error.Message });
        }

        var status = error.Type switch
        {
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return Problem(statusCode: status, title: error.Message);
    }

    protected ActionResult<T> OkOrProblem<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);

    protected ActionResult NoContentOrProblem(Result result) =>
        result.IsSuccess ? NoContent() : ToProblem(result.Error!);
}
