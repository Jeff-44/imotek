using Imotek.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Imotek.API.Services;

/// <summary>
/// Safety net: the application layer validates before touching the domain, so this only
/// fires if a rule is bypassed. Turns it into a 400 instead of a 500.
/// </summary>
internal sealed class DomainValidationExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainValidationException domainException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ValidationProblemDetails(domainException.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = StatusCodes.Status400BadRequest,
            },
            Exception = exception,
        });
    }
}
