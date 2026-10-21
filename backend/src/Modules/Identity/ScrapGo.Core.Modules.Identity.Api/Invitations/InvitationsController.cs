using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Invitations;
using ScrapGo.Core.Modules.Identity.Domain.Users;
using ScrapGo.Core.Shared.Kernel.Paging;
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Invitations;

/// <param name="Email">The invitee's email.</param>
/// <param name="RoleId">An external role.</param>
/// <param name="SupplierRecordIds">One or more Quickbase Suppliers Record IDs.</param>
public sealed record CreateInvitationRequest(string? Email, int RoleId, IReadOnlyList<int>? SupplierRecordIds);

/// <summary>
/// Invitations for external users (ACCESS-MODEL.md section 9). The invitee
/// accepts by signing in and calling <c>POST /api/session</c> with the token.
/// This controller parses, calls <see cref="InvitationService"/> and maps the outcome.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/invitations")]
[Produces("application/json")]
public sealed class InvitationsController(InvitationService invitations) : ControllerBase
{
    /// <summary>Invitations, newest first. Filter: <c>status</c> (Pending, Accepted, Revoked, Expired).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.UsersView, PermissionCatalog.UsersInvite)]
    [ProducesResponseType<PagedResult<InvitationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        await invitations.ListAsync(new ListInvitationsQuery(status, page, pageSize), cancellationToken) is { } result
            ? Ok(result)
            : InvalidRequest(
                $"page must be 1 or more, pageSize 1 to {PageRequest.MaxPageSize}, and status one of: {string.Join(", ", Enum.GetNames<InvitationStatus>())}.");

    /// <summary>The roles an invitation can grant (external roles), for the invite form.</summary>
    [HttpGet("roles")]
    [RequirePermission(PermissionCatalog.UsersInvite)]
    [ProducesResponseType<IReadOnlyList<InvitableRoleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListRoles(CancellationToken cancellationToken) =>
        Ok(await invitations.ListInvitableRolesAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [RequirePermission(PermissionCatalog.UsersView, PermissionCatalog.UsersInvite)]
    [ProducesResponseType<InvitationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken) =>
        await invitations.GetAsync(id, cancellationToken) is { } invitation ? Ok(invitation) : ControllerProblems.NotFound();

    /// <summary>Creates an invitation; the response carries the accept link (it holds the token, shown only here and on resend).</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalog.UsersInvite)]
    [ProducesResponseType<IssuedInvitationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateInvitationRequest? request, CancellationToken cancellationToken)
    {
        var result = await invitations.CreateAsync(
            new CreateInvitationCommand(request?.Email, request?.RoleId ?? 0, request?.SupplierRecordIds), cancellationToken);

        return result is { Outcome: InvitationOutcome.Success, Issued: { } issued }
            ? CreatedAtAction(nameof(Get), new { id = issued.Invitation.Id }, issued)
            : Problem(result.Outcome);
    }

    /// <summary>Issues a new token and a fresh 7-day expiry; the old link stops working.</summary>
    [HttpPost("{id:int}/resend")]
    [RequirePermission(PermissionCatalog.UsersInvite)]
    [ProducesResponseType<IssuedInvitationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resend(int id, CancellationToken cancellationToken)
    {
        var result = await invitations.ResendAsync(id, cancellationToken);

        return result is { Outcome: InvitationOutcome.Success, Issued: { } issued } ? Ok(issued) : Problem(result.Outcome);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionCatalog.UsersInvite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(int id, CancellationToken cancellationToken)
    {
        var outcome = await invitations.RevokeAsync(id, cancellationToken);

        return outcome == InvitationOutcome.Success ? NoContent() : Problem(outcome);
    }

    private static IActionResult Problem(InvitationOutcome outcome) => outcome switch
    {
        InvitationOutcome.RoleNotFound => ProblemResults.Create(
            StatusCodes.Status400BadRequest, "Role not found", "No such active role.", "role_not_found").ToActionResult(),
        InvitationOutcome.RoleNotExternal => ProblemResults.Create(
            StatusCodes.Status400BadRequest, "Role not external",
            "Invitations are for external users; choose an external role.", "role_not_external").ToActionResult(),
        InvitationOutcome.AlreadyPending => ProblemResults.Create(
            StatusCodes.Status409Conflict, "Invitation pending",
            "This email already has a pending invitation. Resend or revoke it.", "invitation_pending").ToActionResult(),
        InvitationOutcome.NotPending => ProblemResults.Create(
            StatusCodes.Status409Conflict, "Invitation not pending",
            "The invitation was already accepted or revoked.", "invitation_not_pending").ToActionResult(),
        InvitationOutcome.NotFound => ControllerProblems.NotFound(),
        _ => InvalidRequest("A valid email, a role id and one or more positive supplier Record IDs are required."),
    };

    private static IActionResult InvalidRequest(string detail) =>
        ProblemResults.Create(StatusCodes.Status400BadRequest, "Invalid invitation request", detail, "invalid_request").ToActionResult();
}
