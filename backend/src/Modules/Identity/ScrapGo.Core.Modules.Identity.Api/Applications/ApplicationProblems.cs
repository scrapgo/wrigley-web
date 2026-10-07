using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Applications;

namespace ScrapGo.Core.Modules.Identity.Api.Applications;

/// <summary>Outcome → ProblemDetails mappings shared by the organization and platform application controllers.</summary>
internal static class ApplicationProblems
{
    public static IActionResult Grant(ControllerBase controller, ApplicationGrantResult result) =>
        result switch
        {
            { Outcome: ApplicationGrantOutcome.Granted or ApplicationGrantOutcome.AlreadyGranted, Grant: { } grant } => controller.Ok(grant),
            { Outcome: ApplicationGrantOutcome.InvalidRequest } => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Invalid grant", "expiresAt, if given, must be in the future.", "invalid_request").ToActionResult(),
            { Outcome: ApplicationGrantOutcome.ApplicationNotAssigned } => ApplicationNotFound(),
            { Outcome: ApplicationGrantOutcome.UserNotFound } => ControllerProblems.NotFound(),
            { Outcome: ApplicationGrantOutcome.TargetNotMember } => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "User is not a member",
                "The user has no active membership in this organization.", "user_not_a_member").ToActionResult(),
            { Outcome: ApplicationGrantOutcome.RoleNotFound } => ProblemResults.Create(
                StatusCodes.Status404NotFound, "Role not found",
                "No such role is available for this application here.", "role_not_found").ToActionResult(),
            { Outcome: ApplicationGrantOutcome.CannotGrantUnheldPermission } => ProblemResults.Create(
                StatusCodes.Status403Forbidden, "Cannot grant unheld permission",
                "The role grants a permission you do not hold in this application.", "cannot_grant_unheld_permission").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ApplicationGrantOutcome)}: {result.Outcome}."),
        };

    public static IActionResult Revoke(ControllerBase controller, ApplicationRevokeOutcome outcome) =>
        outcome switch
        {
            ApplicationRevokeOutcome.Revoked or ApplicationRevokeOutcome.NotHeld => controller.NoContent(),
            ApplicationRevokeOutcome.UserNotFound => ControllerProblems.NotFound(),
            ApplicationRevokeOutcome.LastApplicationAdministrator => ProblemResults.Create(
                StatusCodes.Status409Conflict, "Last application administrator",
                "This user is the application's only active administrator in this organization. Grant another first.",
                "last_application_administrator").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ApplicationRevokeOutcome)}: {outcome}."),
        };

    public static ObjectResult ApplicationNotFound() =>
        ProblemResults.Create(
            StatusCodes.Status404NotFound,
            "Application not found",
            "This organization doesn't have that application.",
            "application_not_found").ToActionResult();
}
