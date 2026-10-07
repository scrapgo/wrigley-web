using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Organizations;
using ScrapGo.Core.Modules.Identity.Domain.Organizations;
using ScrapGo.Core.Shared.Kernel.Paging;
// Api.Permissions (the catalog controller's namespace) would otherwise shadow the domain catalog.
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Organizations;

/// <param name="FirstAdminUserId">An existing user who becomes the organization's first OrganizationAdministrator.</param>
/// <param name="FirstAdminEmail">Instead: someone not yet signed in, invited with OrganizationAdministrator pre-granted.</param>
public sealed record CreatePlatformOrganizationRequest(string? Name, int? FirstAdminUserId, string? FirstAdminEmail = null);

/// <param name="Invitation">Set when the first admin was invited by email: pass the one-time token on to them.</param>
public sealed record RenameOrganizationRequest(string? Name);

public sealed record CreatedOrganizationResponse(int Id, CreatedInvitationDto? Invitation = null);

/// <summary>
/// Platform administration of organizations: list all of them, create one for
/// a customer, deactivate and reactivate.
/// </summary>
/// <remarks>
/// Platform-scoped permissions only. The platform administrator is not a
/// member of these organizations, so the membership guard is skipped
/// (<see cref="PlatformAdministrationAttribute"/>).
/// </remarks>
[ApiController]
[Authorize]
[PlatformAdministration]
[Route("api/admin/organizations")]
[Produces("application/json")]
public sealed class PlatformOrganizationsController(PlatformOrganizationService platformOrganizations) : ControllerBase
{
    /// <summary>Every organization, paged by id. Filters: <c>search</c> (name or slug), <c>status</c>.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.AdminAccess, PlatformScope = true)]
    [ProducesResponseType<PagedResult<OrganizationSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await platformOrganizations.ListAsync(new ListOrganizationsQuery(search, status, page, pageSize), cancellationToken);

        return result switch
        {
            { Outcome: PlatformOrganizationOutcome.Success, Organizations: { } organizations } => Ok(organizations),
            { Outcome: PlatformOrganizationOutcome.InvalidRequest } => ProblemResults.Create(
                StatusCodes.Status400BadRequest,
                "Invalid organization list request",
                $"page must be 1 or more, pageSize 1 to {PageRequest.MaxPageSize}, and status one of: {string.Join(", ", Enum.GetNames<OrganizationStatus>())}.",
                "invalid_request").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(PlatformOrganizationOutcome)}: {result.Outcome}."),
        };
    }

    /// <summary>
    /// Creates an organization and makes the named user its first
    /// OrganizationAdministrator. The caller (a platform administrator) is not
    /// made a member.
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionCatalog.OrganizationCreate, PlatformScope = true)]
    [ProducesResponseType<CreatedOrganizationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CreatePlatformOrganizationRequest request,
        [FromServices] CreateOrganizationHandler handler,
        CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var result = await handler.HandleAsync(
            new CreateOrganizationCommand(uid, request.Name, request.FirstAdminUserId, request.FirstAdminEmail), cancellationToken);

        return result switch
        {
            { Outcome: CreateOrganizationOutcome.Created, OrganizationId: { } id } =>
                Created($"/api/organizations/{id}", new CreatedOrganizationResponse(id, result.Invitation)),

            { Outcome: CreateOrganizationOutcome.InvalidName } => ProblemResults.Create(
                StatusCodes.Status400BadRequest,
                "Invalid organization name",
                "Name must be non-blank and contain at least one letter or digit.",
                "invalid_name").ToActionResult(),

            { Outcome: CreateOrganizationOutcome.InvalidFirstAdmin } => ProblemResults.Create(
                StatusCodes.Status400BadRequest,
                "First administrator required",
                "Give exactly one of firstAdminUserId or firstAdminEmail: who will administer the organization.",
                "invalid_request").ToActionResult(),

            { Outcome: CreateOrganizationOutcome.FirstAdminNotFound } => ProblemResults.Create(
                StatusCodes.Status404NotFound,
                "First administrator not found",
                "No user with that id. They must sign in once before they can be named.",
                "user_not_found").ToActionResult(),

            { Outcome: CreateOrganizationOutcome.DuplicateSlug } => ProblemResults.Create(
                StatusCodes.Status409Conflict,
                "Organization slug already exists",
                "An organization with this name (or an equivalent slug) already exists.",
                "duplicate_slug").ToActionResult(),

            _ => throw new InvalidOperationException($"Unhandled {nameof(CreateOrganizationOutcome)}: {result.Outcome}."),
        };
    }

    /// <summary>Deactivates the organization: its members lose all organization and application access on their next request. Idempotent.</summary>
    [HttpPost("{organizationId:int}/deactivate")]
    [RequirePermission(PermissionCatalog.OrganizationDeactivate, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Deactivate(int organizationId, CancellationToken cancellationToken) =>
        ChangeStatusAsync(organizationId, deactivate: true, cancellationToken);

    /// <summary>Reactivates the organization; everything it had is restored. Idempotent.</summary>
    [HttpPost("{organizationId:int}/reactivate")]
    [RequirePermission(PermissionCatalog.OrganizationDeactivate, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Reactivate(int organizationId, CancellationToken cancellationToken) =>
        ChangeStatusAsync(organizationId, deactivate: false, cancellationToken);

    /// <summary>Renames the organization and regenerates its slug from the new name. Idempotent.</summary>
    [HttpPut("{organizationId:int}")]
    [RequirePermission(PermissionCatalog.OrganizationCreate, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Rename(int organizationId, RenameOrganizationRequest request, CancellationToken cancellationToken) =>
        User.GetIdentityPlatformUid() is { } uid
            ? Map(await platformOrganizations.RenameAsync(uid, organizationId, request.Name, cancellationToken))
            : ControllerProblems.MissingSubjectClaim();

    /// <summary>
    /// Makes an existing user a member and OrganizationAdministrator, and
    /// revokes pending invitations. For fixing a wrong or never-accepted first
    /// administrator. Idempotent.
    /// </summary>
    [HttpPut("{organizationId:int}/administrators/{userId:int}")]
    [RequirePermission(PermissionCatalog.OrganizationCreate, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAdministrator(int organizationId, int userId, CancellationToken cancellationToken) =>
        User.GetIdentityPlatformUid() is { } uid
            ? Map(await platformOrganizations.SetAdministratorAsync(uid, organizationId, userId, cancellationToken))
            : ControllerProblems.MissingSubjectClaim();

    /// <summary>
    /// Permanently deletes a deactivated organization and everything in it.
    /// Its audit history is kept. 409 <c>organization_active</c> unless it was
    /// deactivated first.
    /// </summary>
    [HttpDelete("{organizationId:int}")]
    [RequirePermission(PermissionCatalog.OrganizationDeactivate, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int organizationId, CancellationToken cancellationToken) =>
        User.GetIdentityPlatformUid() is { } uid
            ? Map(await platformOrganizations.DeleteAsync(uid, organizationId, cancellationToken))
            : ControllerProblems.MissingSubjectClaim();

    private IActionResult Map(PlatformOrganizationOutcome outcome) => outcome switch
    {
        PlatformOrganizationOutcome.Success or PlatformOrganizationOutcome.AlreadyInState => NoContent(),
        PlatformOrganizationOutcome.NotFound => ControllerProblems.NotFound(),
        PlatformOrganizationOutcome.InvalidName => ProblemResults.Create(
            StatusCodes.Status400BadRequest,
            "Invalid organization name",
            "The name must contain at least one letter or digit.",
            "invalid_name").ToActionResult(),
        PlatformOrganizationOutcome.UserNotFound => ProblemResults.Create(
            StatusCodes.Status404NotFound,
            "User not found",
            "No user with that id. They must sign in once first.",
            "user_not_found").ToActionResult(),
        PlatformOrganizationOutcome.DuplicateSlug => ProblemResults.Create(
            StatusCodes.Status409Conflict,
            "Organization slug already exists",
            "Another organization already has this name (or an equivalent slug).",
            "duplicate_slug").ToActionResult(),
        PlatformOrganizationOutcome.NotDeactivated => ProblemResults.Create(
            StatusCodes.Status409Conflict,
            "Organization is active",
            "Deactivate the organization before deleting it.",
            "organization_active").ToActionResult(),
        _ => throw new InvalidOperationException($"Unhandled {nameof(PlatformOrganizationOutcome)}: {outcome}."),
    };

    private async Task<IActionResult> ChangeStatusAsync(int organizationId, bool deactivate, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var outcome = deactivate
            ? await platformOrganizations.DeactivateAsync(uid, organizationId, cancellationToken)
            : await platformOrganizations.ReactivateAsync(uid, organizationId, cancellationToken);

        return outcome switch
        {
            PlatformOrganizationOutcome.Success or PlatformOrganizationOutcome.AlreadyInState => NoContent(),
            PlatformOrganizationOutcome.NotFound => ControllerProblems.NotFound(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(PlatformOrganizationOutcome)}: {outcome}."),
        };
    }
}
