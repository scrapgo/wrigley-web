using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ScrapGo.Core.Shared.Infrastructure.Web;

/// <summary>
/// Turns any exception that escapes every endpoint and middleware into
/// ProblemDetails. The message, stack trace and inner exceptions go to the
/// logger only, never into the response.
/// </summary>
/// <remarks>
/// <see cref="UnauthorizedAccessException"/> is an application-level denial
/// (e.g. the Quickbase query service refusing a caller that isn't an active
/// user), so it becomes a 403 with <c>reason=access_denied</c> rather than a 500.
/// </remarks>
public sealed class GenericExceptionHandler(ILogger<GenericExceptionHandler> logger) : IExceptionHandler
{
    public const string AccessDeniedReason = "access_denied";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problemDetails;

        if (exception is UnauthorizedAccessException)
        {
            logger.LogWarning("Access denied processing {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);

            problemDetails = ProblemResults.Create(
                StatusCodes.Status403Forbidden,
                "Access denied",
                "The caller is not allowed to access this resource.",
                AccessDeniedReason);
        }
        else
        {
            logger.LogError(
                exception, "Unhandled exception processing {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
            };
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: ProblemResults.ContentType, cancellationToken);

        return true;
    }
}
