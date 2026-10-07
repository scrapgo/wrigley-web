using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Organizations;
using ScrapGo.Core.Shared.Kernel.Paging;
// Api.Permissions (the catalog controller's namespace) would otherwise shadow the domain catalog.
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Organizations;

/// <summary>
/// One organization's own administration: its detail, its members, renaming
/// it, and adding or removing members.
/// </summary>
/// <remarks>
/// Every route carries <c>{organizationId}</c>, so the membership guard covers
/// it: a caller with no active membership gets 403 <c>no_active_membership</c>,
/// including for an organization id that doesn't exist (a uniform answer, so
/// organization ids can't be probed). Each action then needs, in that
/// organization: <c>User.Read</c> to read, <c>Organization.Update</c> to
/// rename, <c>User.Update</c> to add or remove members. A platform
/// administrator is not let in by platform grants alone.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/organizations/{organizationId:int}")]
[Produces("application/json")]
public sealed class OrganizationDetailController(OrganizationReadService organizationReads) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCatalog.UserRead)]
    [ProducesResponseType<OrganizationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int organizationId, CancellationToken cancellationToken) =>
        await organizationReads.GetAsync(organizationId, cancellationToken) switch
        {
            { Outcome: OrganizationReadOutcome.Success, Organization: { } organization } => Ok(organization),
            { Outcome: OrganizationReadOutcome.NotFound } => ControllerProblems.NotFound(),
            var result => throw new InvalidOperationException($"Unhandled {nameof(OrganizationReadOutcome)}: {result.Outcome}."),
        };

    /// <summary>Active members, paged by user id, each with their roles in this organization only.</summary>
    [HttpGet("members")]
    [RequirePermission(PermissionCatalog.UserRead)]
    [ProducesResponseType<PagedResult<OrganizationMemberDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListMembers(
        int organizationId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        await organizationReads.ListMembersAsync(organizationId, page, pageSize, cancellationToken) switch
        {
            { Outcome: OrganizationReadOutcome.Success, Members: { } members } => Ok(members),
            { Outcome: OrganizationReadOutcome.InvalidRequest } => ProblemResults.Create(
                StatusCodes.Status400BadRequest,
                "Invalid member list request",
                $"page must be 1 or more and pageSize 1 to {PageRequest.MaxPageSize}.",
                "invalid_request").ToActionResult(),
            var result => throw new InvalidOperationException($"Unhandled {nameof(OrganizationReadOutcome)}: {result.Outcome}."),
        };

    /// <summary>Every role the member holds here, organization-level and per application, with expiry.</summary>
    [HttpGet("members/{userId:int}/grants")]
    [RequirePermission(PermissionCatalog.UserRead)]
    [ProducesResponseType<IReadOnlyList<AssignedRoleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMemberGrants(int organizationId, int userId, CancellationToken cancellationToken) =>
        await organizationReads.ListMemberGrantsAsync(organizationId, userId, cancellationToken) is { } grants
            ? Ok(grants)
            : ControllerProblems.NotFound();

    /// <summary>Renames the organization (its slug stays as created) and returns the updated detail.</summary>
    [HttpPut]
    [RequirePermission(PermissionCatalog.OrganizationUpdate)]
    [ProducesResponseType<OrganizationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int organizationId,
        UpdateOrganizationRequest request,
        [FromServices] OrganizationAdminService organizationAdmin,
        CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var result = await organizationAdmin.UpdateAsync(new UpdateOrganizationCommand(uid, organizationId, request.Name), cancellationToken);

        return result switch
        {
            { Outcome: UpdateOrganizationOutcome.Updated, Organization: { } organization } => Ok(organization),
            { Outcome: UpdateOrganizationOutcome.InvalidName } => ProblemResults.Create(
                StatusCodes.Status400BadRequest,
                "Invalid organization name",
                "Name must be non-blank and contain at least one letter or digit.",
                "invalid_name").ToActionResult(),
            { Outcome: UpdateOrganizationOutcome.Updated or UpdateOrganizationOutcome.NotFound } => ControllerProblems.NotFound(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(UpdateOrganizationOutcome)}: {result.Outcome}."),
        };
    }

    /// <summary>
    /// Adds the user as a member. Grants no roles: assign those explicitly.
    /// Idempotent: an existing active member returns 204 without writing anything.
    /// </summary>
    [HttpPost("members/{userId:int}")]
    [RequirePermission(PermissionCatalog.UserUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMember(
        int organizationId,
        int userId,
        [FromServices] OrganizationAdminService organizationAdmin,
        CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        return await organizationAdmin.AddMemberAsync(new MembershipCommand(uid, organizationId, userId), cancellationToken) switch
        {
            AddMemberOutcome.Added or AddMemberOutcome.AlreadyMember => NoContent(),
            AddMemberOutcome.UserNotFound => ControllerProblems.NotFound(),
            var outcome => throw new InvalidOperationException($"Unhandled {nameof(AddMemberOutcome)}: {outcome}."),
        };
    }

    /// <summary>
    /// Removes the member and every role they hold in this organization.
    /// Idempotent: a user who is not a member returns 204 without writing anything.
    /// </summary>
    [HttpDelete("members/{userId:int}")]
    [RequirePermission(PermissionCatalog.UserUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(
        int organizationId,
        int userId,
        [FromServices] OrganizationAdminService organizationAdmin,
        CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        return await organizationAdmin.RemoveMemberAsync(new MembershipCommand(uid, organizationId, userId), cancellationToken) switch
        {
            RemoveMemberOutcome.Removed or RemoveMemberOutcome.NotMember => NoContent(),
            RemoveMemberOutcome.UserNotFound => ControllerProblems.NotFound(),
            RemoveMemberOutcome.LastOrganizationAdministrator => ControllerProblems.LastOrganizationAdministrator(),
            var outcome => throw new InvalidOperationException($"Unhandled {nameof(RemoveMemberOutcome)}: {outcome}."),
        };
    }
}
