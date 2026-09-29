using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ScrapGo.Core.Shared.Infrastructure.Web;

/// <summary>
/// RFC 7807 ProblemDetails with a machine-readable <c>reason</c> extension
/// member, so clients never have to parse the free-text <c>detail</c> to tell
/// denials apart.
/// </summary>
public static class ProblemResults
{
    public const string ContentType = "application/problem+json";
    public const string ReasonKey = "reason";

    public static ProblemDetails Create(int statusCode, string title, string detail, string? reason = null)
    {
        var problemDetails = new ProblemDetails { Status = statusCode, Title = title, Detail = detail };

        if (reason is not null)
        {
            problemDetails.Extensions[ReasonKey] = reason;
        }

        return problemDetails;
    }

    /// <summary>For controllers.</summary>
    public static ObjectResult ToActionResult(this ProblemDetails problemDetails) =>
        new(problemDetails)
        {
            StatusCode = problemDetails.Status,
            ContentTypes = { ContentType },
        };

    /// <summary>For middleware and authentication events that write the response directly.</summary>
    public static Task WriteAsync(HttpResponse response, ProblemDetails problemDetails)
    {
        response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        return response.WriteAsJsonAsync(problemDetails, options: null, contentType: ContentType);
    }
}
