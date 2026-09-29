using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ScrapGo.Core.Shared.Infrastructure.Web;

/// <summary>
/// Turns any exception that escapes every endpoint and middleware into a
/// generic 500 ProblemDetails. The message, stack trace and inner exceptions
/// go to the logger only, never into the response.
/// </summary>
public sealed class GenericExceptionHandler(ILogger<GenericExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(
            exception, "Unhandled exception processing {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
        };

        await httpContext.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: ProblemResults.ContentType, cancellationToken);

        return true;
    }
}
