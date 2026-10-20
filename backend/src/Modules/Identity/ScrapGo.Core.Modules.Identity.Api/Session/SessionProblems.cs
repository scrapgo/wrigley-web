using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Session;

namespace ScrapGo.Core.Modules.Identity.Api.Session;

internal static class SessionProblems
{
    /// <summary>The 403 <c>reason</c> for a sign-in with no user and no route to one.</summary>
    public const string NoAccessReason = StartSessionHandler.NoAccessReason;

    public static IActionResult NoAccess() =>
        ProblemResults.Create(
            StatusCodes.Status403Forbidden,
            "No access",
            "This account has no access to ScrapGo.",
            NoAccessReason).ToActionResult();

    public static IActionResult AccountDisabled() =>
        ProblemResults.Create(
            StatusCodes.Status403Forbidden,
            "Account disabled",
            "This account has been disabled.",
            EvaluateUserStatusGateHandler.AccountDisabledReason).ToActionResult();
}
