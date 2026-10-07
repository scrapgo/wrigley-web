using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Organizations;
// Api.Permissions (the catalog controller's namespace) would otherwise shadow the domain catalog.
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Organizations;

/// <param name="Grants">Roles to grant on acceptance; <c>applicationId</c> set for an application role.</param>
/// <param name="ExpiresInDays">1 to 30; 7 when omitted.</param>
public sealed record CreateInvitationRequest(string? Email, IReadOnlyList<InvitationGrantRequest>? Grants, int? ExpiresInDays);

public sealed record AcceptInvitationRequest(string? Token);

public sealed record AcceptedInvitationResponse(int OrganizationId, int GrantedCount, int SkippedGrants);

/// <summary>
/// Invitations into one organization: invite by email with pre-grants, list
/// pending, revoke. Under the membership guard; inviting needs
/// <c>User.Update</c>, and each pre-grant is checked like a direct grant
/// (organization roles: <c>Role.Assign</c>; application roles:
/// <c>Application.ManageAccess</c>; the escalation guard for both).
/// </summary>
[ApiController]
[Authorize]
[Route("api/organizations/{organizationId:int}/invitations")]
[Produces("application/json")]
public sealed class InvitationsController(InvitationService invitationService) : ControllerBase
{
    /// <summary>Invites an email address. The response carries the one-time token: it is not retrievable again.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalog.UserUpdate)]
    [ProducesResponseType<CreatedInvitationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(int organizationId, CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var result = await invitationService.CreateAsync(
            new CreateInvitationCommand(uid, ActingAsPlatformAdmin: false, organizationId, request.Email, request.Grants ?? [], request.ExpiresInDays),
            cancellationToken);

        return result switch
        {
            { Outcome: InvitationOutcome.Created, Invitation: { } invitation } =>
                Created($"/api/organizations/{organizationId}/invitations", invitation),
            { Outcome: InvitationOutcome.InvalidRequest } => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Invalid invitation",
                "A valid email is required, grants must be distinct, and expiresInDays must be 1 to 30.", "invalid_request").ToActionResult(),
            { Outcome: InvitationOutcome.InvitationPending } => ProblemResults.Create(
                StatusCodes.Status409Conflict, "Invitation pending",
                "This email already has a pending invitation here. Revoke it first.", "invitation_pending").ToActionResult(),
            { Outcome: InvitationOutcome.RoleNotFound } => ProblemResults.Create(
                StatusCodes.Status404NotFound, "Role not found", "A pre-granted role isn't available here.", "role_not_found").ToActionResult(),
            { Outcome: InvitationOutcome.ApplicationNotAssigned } => ProblemResults.Create(
                StatusCodes.Status404NotFound, "Application not found",
                "This organization doesn't have a pre-granted role's application.", "application_not_found").ToActionResult(),
            { Outcome: InvitationOutcome.Forbidden } => ControllerProblems.MissingPermission(
                "Pre-granting needs Role.Assign (organization roles) or Application.ManageAccess (application roles)."),
            { Outcome: InvitationOutcome.CannotGrantUnheldPermission } => ProblemResults.Create(
                StatusCodes.Status403Forbidden, "Cannot grant unheld permission",
                "A pre-granted role carries a permission you don't hold there.", "cannot_grant_unheld_permission").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(InvitationOutcome)}: {result.Outcome}."),
        };
    }

    /// <summary>Pending invitations (expired ones are shown as Expired). Tokens are never returned.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.UserRead)]
    [ProducesResponseType<IReadOnlyList<InvitationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListPending(int organizationId, CancellationToken cancellationToken) =>
        Ok(await invitationService.ListPendingAsync(organizationId, cancellationToken));

    /// <summary>Revokes a pending invitation. Idempotent.</summary>
    [HttpDelete("{invitationId:int}")]
    [RequirePermission(PermissionCatalog.UserUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(int organizationId, int invitationId, CancellationToken cancellationToken) =>
        User.GetIdentityPlatformUid() is not { } uid
            ? ControllerProblems.MissingSubjectClaim()
            : await invitationService.RevokeAsync(uid, organizationId, invitationId, cancellationToken)
                ? NoContent()
                : ControllerProblems.NotFound();
}

/// <summary>
/// Accepting an invitation, as the invited person: any signed-in user whose
/// token carries the invited email, verified by the identity provider.
/// </summary>
[ApiController]
[Authorize]
[Route("api/invitations")]
[Produces("application/json")]
public sealed class InvitationAcceptanceController(InvitationService invitationService) : ControllerBase
{
    [HttpPost("accept")]
    [ProducesResponseType<AcceptedInvitationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Accept(AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var emailVerified = string.Equals(User.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        var result = await invitationService.AcceptAsync(
            uid, User.FindFirst("email")?.Value, emailVerified, User.FindFirst("hd")?.Value, request.Token, cancellationToken);

        return result switch
        {
            { Outcome: AcceptInvitationOutcome.Accepted, OrganizationId: { } organizationId } =>
                Ok(new AcceptedInvitationResponse(organizationId, result.GrantedCount, result.SkippedGrants)),
            { Outcome: AcceptInvitationOutcome.NotFound } => ProblemResults.Create(
                StatusCodes.Status404NotFound, "Invitation not found",
                "No open invitation matches this token.", "invitation_not_found").ToActionResult(),
            { Outcome: AcceptInvitationOutcome.Expired } => ProblemResults.Create(
                StatusCodes.Status410Gone, "Invitation expired", "Ask for a new invitation.", "invitation_expired").ToActionResult(),
            { Outcome: AcceptInvitationOutcome.EmailNotVerified } => ProblemResults.Create(
                StatusCodes.Status403Forbidden, "Email not verified",
                "Verify your email address with your sign-in provider, then accept again.", "email_not_verified").ToActionResult(),
            { Outcome: AcceptInvitationOutcome.EmailMismatch } => ProblemResults.Create(
                StatusCodes.Status403Forbidden, "Different email",
                "This invitation is for a different email address than the one you signed in with.", "invitation_email_mismatch").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(AcceptInvitationOutcome)}: {result.Outcome}."),
        };
    }
}
