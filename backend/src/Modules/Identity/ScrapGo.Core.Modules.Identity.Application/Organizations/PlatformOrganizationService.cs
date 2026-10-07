using System.Text.Json;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Application.Organizations;

/// <param name="Status">Optional; must name an <see cref="OrganizationStatus"/> (case-insensitive).</param>
public sealed record ListOrganizationsQuery(string? Search, string? Status, int? Page, int? PageSize);

public enum PlatformOrganizationOutcome
{
    Success,

    /// <summary>The organization already had that status. Nothing was written.</summary>
    AlreadyInState,

    /// <summary>A page value out of range, or an unknown status.</summary>
    InvalidRequest,

    NotFound,

    /// <summary>The name is blank, or has no letter or digit.</summary>
    InvalidName,

    /// <summary>No user with that id: they must sign in once first.</summary>
    UserNotFound,

    /// <summary>Only a deactivated organization can be deleted.</summary>
    NotDeactivated,

    /// <summary>Another organization already has the slug the new name produces.</summary>
    DuplicateSlug,
}

public sealed record PlatformOrganizationListResult(PlatformOrganizationOutcome Outcome, PagedResult<OrganizationSummaryDto>? Organizations = null);

/// <summary>
/// Platform-wide organization administration: list every organization;
/// rename one, set its administrator, deactivate, reactivate, and delete a
/// deactivated one permanently.
/// </summary>
/// <remarks>
/// The routes require platform-scoped permissions (<c>Admin.Access</c>,
/// <c>Organization.Deactivate</c>), checked before these methods run. They sit
/// outside the membership guard because a platform administrator is not a
/// member of the organizations they administer.
/// </remarks>
public sealed class PlatformOrganizationService(
    IUserRepository users,
    IOrganizationRepository organizations,
    IRoleRepository roles,
    IInvitationRepository invitations,
    IAuthorizationQueries authorization,
    IPermissionCache permissionCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public async Task<PlatformOrganizationListResult> ListAsync(ListOrganizationsQuery query, CancellationToken cancellationToken)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        if (!page.IsValid || !TryParseStatus(query.Status, out var status))
        {
            return new(PlatformOrganizationOutcome.InvalidRequest);
        }

        return new(PlatformOrganizationOutcome.Success,
            await authorization.ListOrganizationsAsync(new OrganizationListFilter(query.Search, status), page, cancellationToken));
    }

    /// <summary>
    /// Blocks all organization- and application-scoped access for its members
    /// on their next request. Nothing is deleted.
    /// </summary>
    public Task<PlatformOrganizationOutcome> DeactivateAsync(string actorUid, int organizationId, CancellationToken cancellationToken) =>
        ChangeStatusAsync(actorUid, organizationId, OrganizationStatus.Disabled, cancellationToken);

    /// <summary>Restores every membership, role and grant exactly as it was before deactivation.</summary>
    public Task<PlatformOrganizationOutcome> ReactivateAsync(string actorUid, int organizationId, CancellationToken cancellationToken) =>
        ChangeStatusAsync(actorUid, organizationId, OrganizationStatus.Active, cancellationToken);

    /// <summary>
    /// Renames an organization the platform admin need not belong to, and
    /// regenerates its slug from the new name. Same rule as creating: a letter
    /// or digit is required, and the slug must be unique.
    /// </summary>
    public async Task<PlatformOrganizationOutcome> RenameAsync(
        string actorUid, int organizationId, string? name, CancellationToken cancellationToken)
    {
        if (Organization.ToSlug(name).Length == 0)
        {
            return PlatformOrganizationOutcome.InvalidName;
        }

        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await organizations.GetByIdAsync(organizationId, ct) is not { } organization)
                {
                    return PlatformOrganizationOutcome.NotFound;
                }

                var (before, beforeSlug) = (organization.Name, organization.Slug);
                organization.RenameWithSlug(name!, timeProvider.GetUtcNow());
                if (organization.Name == before && organization.Slug == beforeSlug)
                {
                    return PlatformOrganizationOutcome.AlreadyInState;
                }

                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.OrganizationUpdated,
                    UserId: actorUserId,
                    OrganizationId: organizationId,
                    Metadata: JsonSerializer.Serialize(new
                    {
                        before,
                        after = organization.Name,
                        slugBefore = beforeSlug,
                        slugAfter = organization.Slug,
                        byPlatformAdmin = true,
                    })));
                await unitOfWork.SaveChangesAsync(ct);

                return PlatformOrganizationOutcome.Success;
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.OrganizationSlug)
        {
            return PlatformOrganizationOutcome.DuplicateSlug;
        }
    }

    /// <summary>
    /// Makes an existing user an active member and OrganizationAdministrator,
    /// and revokes the organization's pending invitations. It fixes an
    /// organization created with the wrong first administrator or an invitation
    /// nobody will accept. Idempotent.
    /// </summary>
    public async Task<PlatformOrganizationOutcome> SetAdministratorAsync(
        string actorUid, int organizationId, int userId, CancellationToken cancellationToken)
    {
        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);
        if (await users.GetByIdAsync(userId, cancellationToken) is null)
        {
            return PlatformOrganizationOutcome.UserNotFound;
        }

        var outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await organizations.GetByIdAsync(organizationId, ct) is null)
            {
                return PlatformOrganizationOutcome.NotFound;
            }

            var now = timeProvider.GetUtcNow();
            var changed = false;

            switch (await organizations.FindMembershipAsync(userId, organizationId, ct))
            {
                case null:
                    organizations.AddMembership(OrganizationMembership.Create(userId, organizationId, now));
                    auditLog.Record(new AuditEvent(
                        IdentityAuditEventTypes.MembershipAdded,
                        UserId: actorUserId,
                        OrganizationId: organizationId,
                        Metadata: JsonSerializer.Serialize(new { targetUserId = userId, reactivated = false, byPlatformAdmin = true })));
                    changed = true;
                    break;
                case { Status: MembershipStatus.Disabled } membership:
                    membership.Enable(now);
                    auditLog.Record(new AuditEvent(
                        IdentityAuditEventTypes.MembershipAdded,
                        UserId: actorUserId,
                        OrganizationId: organizationId,
                        Metadata: JsonSerializer.Serialize(new { targetUserId = userId, reactivated = true, byPlatformAdmin = true })));
                    changed = true;
                    break;
            }

            var roleId = await roles.GetPlatformRoleIdAsync(DefaultRoleNames.OrganizationAdministrator, ct);
            if (await roles.FindUserRoleAsync(userId, roleId, organizationId, ct) is null)
            {
                roles.AddUserRole(UserRole.Assign(userId, roleId, organizationId, now));
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.RoleAssigned,
                    UserId: actorUserId,
                    OrganizationId: organizationId,
                    Metadata: JsonSerializer.Serialize(new { targetUserId = userId, roleId, organizationId })));
                changed = true;
            }

            // ListPendingAsync is untracked; revoke through the tracked entity.
            foreach (var pending in await invitations.ListPendingAsync(organizationId, ct))
            {
                if (await invitations.FindAsync(organizationId, pending.Id, ct) is not { } invitation)
                {
                    continue;
                }

                invitation.Revoke(now);
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.InvitationRevoked,
                    UserId: actorUserId,
                    OrganizationId: organizationId,
                    Metadata: JsonSerializer.Serialize(new { invitationId = invitation.Id, reason = "administrator_set" })));
                changed = true;
            }

            await unitOfWork.SaveChangesAsync(ct);
            return changed ? PlatformOrganizationOutcome.Success : PlatformOrganizationOutcome.AlreadyInState;
        }, cancellationToken);

        if (outcome == PlatformOrganizationOutcome.Success)
        {
            await permissionCache.InvalidateOrganizationAsync(organizationId, cancellationToken);
        }

        return outcome;
    }

    /// <summary>
    /// Permanently deletes a deactivated organization and everything in it
    /// (see <see cref="IOrganizationRepository.DeleteWithDependentsAsync"/>).
    /// Requiring deactivation first makes it a deliberate two-step action.
    /// Its audit history is kept, plus an <c>organization_deleted</c> entry.
    /// </summary>
    public async Task<PlatformOrganizationOutcome> DeleteAsync(string actorUid, int organizationId, CancellationToken cancellationToken)
    {
        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);

        var outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await organizations.GetByIdAsync(organizationId, ct) is not { } organization)
            {
                return PlatformOrganizationOutcome.NotFound;
            }

            if (organization.Status != OrganizationStatus.Disabled)
            {
                return PlatformOrganizationOutcome.NotDeactivated;
            }

            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.OrganizationDeleted,
                UserId: actorUserId,
                OrganizationId: organizationId,
                Metadata: JsonSerializer.Serialize(new { name = organization.Name, slug = organization.Slug })));
            await unitOfWork.SaveChangesAsync(ct);
            await organizations.DeleteWithDependentsAsync(organizationId, ct);

            return PlatformOrganizationOutcome.Success;
        }, cancellationToken);

        if (outcome == PlatformOrganizationOutcome.Success)
        {
            await permissionCache.InvalidateOrganizationAsync(organizationId, cancellationToken);
        }

        return outcome;
    }

    private async Task<PlatformOrganizationOutcome> ChangeStatusAsync(
        string actorUid, int organizationId, OrganizationStatus status, CancellationToken cancellationToken)
    {
        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);

        var outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await organizations.GetByIdAsync(organizationId, ct) is not { } organization)
            {
                return PlatformOrganizationOutcome.NotFound;
            }

            if (organization.Status == status)
            {
                return PlatformOrganizationOutcome.AlreadyInState;
            }

            var now = timeProvider.GetUtcNow();
            if (status == OrganizationStatus.Disabled)
            {
                organization.Disable(now);
            }
            else
            {
                organization.Reactivate(now);
            }

            auditLog.Record(new AuditEvent(
                status == OrganizationStatus.Disabled
                    ? IdentityAuditEventTypes.OrganizationDeactivated
                    : IdentityAuditEventTypes.OrganizationReactivated,
                UserId: actorUserId,
                OrganizationId: organizationId,
                Metadata: JsonSerializer.Serialize(new { name = organization.Name })));
            await unitOfWork.SaveChangesAsync(ct);

            return PlatformOrganizationOutcome.Success;
        }, cancellationToken);

        if (outcome == PlatformOrganizationOutcome.Success)
        {
            await permissionCache.InvalidateOrganizationAsync(organizationId, cancellationToken);
        }

        return outcome;
    }

    /// <summary>Names only ("Active", "disabled"); numeric strings are rejected rather than cast to the enum.</summary>
    private static bool TryParseStatus(string? value, out OrganizationStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.Any(char.IsDigit) || !Enum.TryParse<OrganizationStatus>(value, ignoreCase: true, out var parsed))
        {
            return false;
        }

        status = parsed;
        return true;
    }
}
