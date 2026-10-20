using Microsoft.AspNetCore.Mvc;

namespace ScrapGo.Core.Modules.Identity.Api;

/// <summary>ProblemDetails results shared by this module's controllers.</summary>
internal static class ControllerProblems
{
    /// <summary>
    /// Defensive only. [Authorize] already guarantees a bearer-authenticated
    /// principal, and GCIP ID tokens always carry <c>sub</c>. This exists so a
    /// future change to the auth scheme fails loudly.
    /// </summary>
    public static ObjectResult MissingSubjectClaim() =>
        ProblemResults.Create(
            StatusCodes.Status401Unauthorized,
            "Missing subject claim",
            "The authenticated token does not carry a 'sub' claim.").ToActionResult();

    /// <summary>The caller lacks the permission.</summary>
    public static ObjectResult MissingPermission(string detail) =>
        ProblemResults.Create(
            StatusCodes.Status403Forbidden,
            "Missing permission",
            detail,
            "missing_permission").ToActionResult();

    public static ObjectResult NotFound() =>
        ProblemResults.Create(StatusCodes.Status404NotFound, "Not found", "The requested resource does not exist.").ToActionResult();
}
