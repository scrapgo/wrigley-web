using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Applications;
// Api.Permissions (the catalog controller's namespace) would otherwise shadow the domain catalog.
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Applications;

/// <param name="Status"><c>Active</c> or <c>Retired</c>.</param>
public sealed record ChangeCatalogStatusRequest(string? Status);

/// <summary>
/// The application catalog: platform applications, their modules and the
/// permissions each module owns. Definitions are code (ORG-APP-MODULE-MODEL.md,
/// Decision 2); a platform administrator can only retire or reactivate.
/// </summary>
[ApiController]
[Authorize]
[Route("api/catalog/applications")]
[Produces("application/json")]
public sealed class CatalogController(ApplicationCatalogService catalog) : ControllerBase
{
    /// <summary>Every catalog application, with its modules and their permissions. Any signed-in user may read it.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CatalogApplicationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await catalog.ListAsync(cancellationToken));

    /// <summary>Retires or reactivates an application platform-wide. Retired applications resolve to deny everywhere. Idempotent.</summary>
    [HttpPut("{applicationId:int}")]
    [RequirePermission(PermissionCatalog.CatalogManage, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeApplicationStatus(
        int applicationId, ChangeCatalogStatusRequest request, CancellationToken cancellationToken) =>
        User.GetIdentityPlatformUid() is { } uid
            ? MapOutcome(await catalog.ChangeApplicationStatusAsync(uid, applicationId, request.Status, cancellationToken))
            : ControllerProblems.MissingSubjectClaim();

    /// <summary>Retires or reactivates one module platform-wide. Idempotent.</summary>
    [HttpPut("{applicationId:int}/modules/{moduleId:int}")]
    [RequirePermission(PermissionCatalog.CatalogManage, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeModuleStatus(
        int applicationId, int moduleId, ChangeCatalogStatusRequest request, CancellationToken cancellationToken) =>
        User.GetIdentityPlatformUid() is { } uid
            ? MapOutcome(await catalog.ChangeModuleStatusAsync(uid, applicationId, moduleId, request.Status, cancellationToken))
            : ControllerProblems.MissingSubjectClaim();

    private IActionResult MapOutcome(CatalogChangeOutcome outcome) =>
        outcome switch
        {
            CatalogChangeOutcome.Changed or CatalogChangeOutcome.AlreadyInState => NoContent(),
            CatalogChangeOutcome.InvalidRequest => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Invalid status", "status must be Active or Retired.", "invalid_request").ToActionResult(),
            CatalogChangeOutcome.NotFound => ControllerProblems.NotFound(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(CatalogChangeOutcome)}: {outcome}."),
        };
}
