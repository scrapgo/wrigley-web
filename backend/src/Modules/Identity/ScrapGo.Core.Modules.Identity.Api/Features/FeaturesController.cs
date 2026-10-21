using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Features;
using ScrapGo.Core.Modules.Identity.Domain.Features;
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Features;

/// <param name="State">Off, On or Targeted.</param>
public sealed record SetFeatureStateRequest(string? State);

/// <param name="UserIds">Every user the feature should target; others are removed.</param>
/// <param name="RoleIds">Every role the feature should target; others are removed.</param>
public sealed record SetFeatureTargetsRequest(IReadOnlyList<int>? UserIds, IReadOnlyList<int>? RoleIds);

/// <summary>
/// Feature toggles (ACCESS-MODEL.md section 9). Reads need <c>features.view</c>,
/// changes <c>features.manage</c>. A change applies to everyone within the
/// access cache's 30 seconds (immediately on this instance).
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/features")]
[Produces("application/json")]
public sealed class FeaturesController(FeatureAdminService features) : ControllerBase
{
    /// <summary>Every feature, its state, the permissions it owns and its targets.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.FeaturesView)]
    [ProducesResponseType<IReadOnlyList<FeatureDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await features.ListAsync(cancellationToken));

    /// <summary>Sets the toggle. 409 <c>feature_locked</c> for a locked feature.</summary>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCatalog.FeaturesManage)]
    [ProducesResponseType<FeatureDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetState(int id, [FromBody] SetFeatureStateRequest? request, CancellationToken cancellationToken) =>
        ToResult(await features.SetStateAsync(new SetFeatureStateCommand(id, request?.State), cancellationToken));

    /// <summary>Sets who a Targeted feature is on for: these users, and every holder of these roles.</summary>
    [HttpPut("{id:int}/targets")]
    [RequirePermission(PermissionCatalog.FeaturesManage)]
    [ProducesResponseType<FeatureDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetTargets(int id, [FromBody] SetFeatureTargetsRequest? request, CancellationToken cancellationToken) =>
        ToResult(await features.SetTargetsAsync(new SetFeatureTargetsCommand(id, request?.UserIds, request?.RoleIds), cancellationToken));

    private IActionResult ToResult(FeatureAdminResult result) => result switch
    {
        { Outcome: FeatureAdminOutcome.Success, Feature: { } feature } => Ok(feature),
        { Outcome: FeatureAdminOutcome.InvalidRequest } => ProblemResults.Create(
            StatusCodes.Status400BadRequest, "Invalid feature request",
            $"state must be one of {string.Join(", ", Enum.GetNames<FeatureState>())}; targets need userIds and roleIds lists of positive ids.",
            "invalid_request").ToActionResult(),
        { Outcome: FeatureAdminOutcome.Locked } => ProblemResults.Create(
            StatusCodes.Status409Conflict, "Feature locked",
            "This feature is locked: it is always On and has no targets.", "feature_locked").ToActionResult(),
        { Outcome: FeatureAdminOutcome.UserNotFound } => ProblemResults.Create(
            StatusCodes.Status400BadRequest, "User not found", "One of the user ids doesn't exist.", "user_not_found").ToActionResult(),
        { Outcome: FeatureAdminOutcome.RoleNotFound } => ProblemResults.Create(
            StatusCodes.Status400BadRequest, "Role not found", "One of the role ids doesn't exist.", "role_not_found").ToActionResult(),
        _ => ControllerProblems.NotFound(),
    };
}
