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

    /// <summary>The caller has no active membership in the organization, or lacks the permission there.</summary>
    public static ObjectResult MissingPermission(string detail) =>
        ProblemResults.Create(
            StatusCodes.Status403Forbidden,
            "Missing permission",
            detail,
            "missing_permission").ToActionResult();

    /// <summary>Escalation guard: callers may not change a role they hold themselves.</summary>
    public static ObjectResult CannotModifyOwnRole() =>
        ProblemResults.Create(
            StatusCodes.Status403Forbidden,
            "Cannot modify own role",
            "You hold this role yourself; another administrator must change it.",
            "cannot_modify_own_role").ToActionResult();

    /// <summary>Lock-out protection: an organization always keeps one active OrganizationAdministrator.</summary>
    public static ObjectResult LastOrganizationAdministrator() =>
        ProblemResults.Create(
            StatusCodes.Status409Conflict,
            "Last organization administrator",
            "This user is the organization's only active OrganizationAdministrator. Assign another first.",
            "last_organization_administrator").ToActionResult();

    public static ObjectResult NotFound() =>
        ProblemResults.Create(StatusCodes.Status404NotFound, "Not found", "The requested resource does not exist.").ToActionResult();
}
