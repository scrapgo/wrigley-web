using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Organizations;

/// <param name="ActorUid">The caller's UID from the validated token's <c>sub</c> claim.</param>
/// <param name="OrganizationId">From the route.</param>
/// <param name="UserId">The member to add or remove, from the route.</param>
public sealed record MembershipCommand(string ActorUid, int OrganizationId, int UserId);

public enum UpdateOrganizationOutcome
{
    Updated,

    /// <summary>The name is blank, or has no letter or digit.</summary>
    InvalidName,

    NotFound,
}

public sealed record UpdateOrganizationResult(UpdateOrganizationOutcome Outcome, OrganizationDetailDto? Organization = null);

public enum AddMemberOutcome
{
    Added,

    /// <summary>The user already had an active membership. Nothing was written.</summary>
    AlreadyMember,

    UserNotFound,
}

public enum RemoveMemberOutcome
{
    Removed,

    /// <summary>The user had no membership in the organization. Nothing was written.</summary>
    NotMember,

    UserNotFound,

    /// <summary>The user is the organization's only active OrganizationAdministrator.</summary>
    LastOrganizationAdministrator,
}

/// <summary>
/// Writes within one organization: rename it, add members, remove members.
/// </summary>
/// <remarks>
/// <para>
/// Authorization happens before these methods run, on the route's
/// <c>{organizationId}</c>: the membership guard, then
/// <c>Organization.Update</c> (rename) or <c>User.Update</c> (membership)
/// in that organization. Every change is one transaction with its audit rows.
/// </para>
/// <para>
/// Membership and roles stay separate: adding a member grants nothing
/// (deny-by-default; roles are assigned explicitly). Removing a member also
/// revokes every role they hold in that organization, in the same
/// transaction, each audited, because a role without a membership would
/// otherwise come back to life if they were re-added.
/// </para>
/// </remarks>
public sealed class OrganizationAdminService(
    IUserRepository users,
    IOrganizationRepository organizations,
    IRoleRepository roles,
    IAuthorizationQueries authorization,
    IPermissionCache permissionCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public async Task<UpdateOrganizationResult> UpdateAsync(UpdateOrganizationCommand command, CancellationToken cancellationToken)
    {
        if (!command.IsWellFormed)
        {
            return new(UpdateOrganizationOutcome.InvalidName);
        }

        var actorUserId = await users.GetIdByUidAsync(command.ActorUid, cancellationToken);

        var outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await organizations.GetByIdAsync(command.OrganizationId, ct) is not { } organization)
            {
                return UpdateOrganizationOutcome.NotFound;
            }

            var before = organization.Name;
            organization.Rename(command.Name!, timeProvider.GetUtcNow());
            if (organization.Name == before)
            {
                // Idempotent: the same name again changes nothing, so audits nothing.
                return UpdateOrganizationOutcome.Updated;
            }

            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.OrganizationUpdated,
                UserId: actorUserId,
                OrganizationId: organization.Id,
                Metadata: JsonSerializer.Serialize(new { before = new { name = before }, after = new { name = organization.Name } })));
            await unitOfWork.SaveChangesAsync(ct);

            return UpdateOrganizationOutcome.Updated;
        }, cancellationToken);

        return outcome == UpdateOrganizationOutcome.Updated
            ? new(outcome, await authorization.GetOrganizationDetailAsync(command.OrganizationId, cancellationToken))
            : new(outcome);
    }

    public async Task<AddMemberOutcome> AddMemberAsync(MembershipCommand command, CancellationToken cancellationToken)
    {
        if (await users.GetByIdAsync(command.UserId, cancellationToken) is null)
        {
            return AddMemberOutcome.UserNotFound;
        }

        var actorUserId = await users.GetIdByUidAsync(command.ActorUid, cancellationToken);

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var now = timeProvider.GetUtcNow();
                var reactivated = false;

                switch (await organizations.FindMembershipAsync(command.UserId, command.OrganizationId, ct))
                {
                    case { Status: MembershipStatus.Active }:
                        return AddMemberOutcome.AlreadyMember;
                    case { } disabled:
                        disabled.Enable(now);
                        reactivated = true;
                        break;
                    default:
                        organizations.AddMembership(OrganizationMembership.Create(command.UserId, command.OrganizationId, now));
                        break;
                }

                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.MembershipAdded,
                    UserId: actorUserId,
                    OrganizationId: command.OrganizationId,
                    Metadata: JsonSerializer.Serialize(new { targetUserId = command.UserId, reactivated })));
                await unitOfWork.SaveChangesAsync(ct);

                return AddMemberOutcome.Added;
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.OrganizationMembership)
        {
            // A concurrent request added them first.
            return AddMemberOutcome.AlreadyMember;
        }
    }

    public async Task<RemoveMemberOutcome> RemoveMemberAsync(MembershipCommand command, CancellationToken cancellationToken)
    {
        if (await users.GetByIdAsync(command.UserId, cancellationToken) is null)
        {
            return RemoveMemberOutcome.UserNotFound;
        }

        var actorUserId = await users.GetIdByUidAsync(command.ActorUid, cancellationToken);

        var outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await organizations.FindMembershipAsync(command.UserId, command.OrganizationId, ct) is not { } membership)
            {
                return RemoveMemberOutcome.NotMember;
            }

            if (await IsLastOrganizationAdministratorAsync(command.UserId, command.OrganizationId, ct))
            {
                return RemoveMemberOutcome.LastOrganizationAdministrator;
            }

            foreach (var assignment in await roles.ListUserRoleAssignmentsAsync(command.UserId, command.OrganizationId, ct))
            {
                roles.RemoveUserRole(assignment);
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.RoleRevoked,
                    UserId: actorUserId,
                    OrganizationId: command.OrganizationId,
                    Metadata: JsonSerializer.Serialize(new
                    {
                        targetUserId = command.UserId,
                        roleId = assignment.RoleId,
                        organizationId = command.OrganizationId,
                        reason = IdentityAuditEventTypes.MembershipRemoved,
                    })));
            }

            organizations.RemoveMembership(membership);
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.MembershipRemoved,
                UserId: actorUserId,
                OrganizationId: command.OrganizationId,
                Metadata: JsonSerializer.Serialize(new { targetUserId = command.UserId })));
            await unitOfWork.SaveChangesAsync(ct);

            return RemoveMemberOutcome.Removed;
        }, cancellationToken);

        if (outcome == RemoveMemberOutcome.Removed)
        {
            await permissionCache.InvalidateAsync(new PermissionScope(command.UserId, command.OrganizationId), cancellationToken);
        }

        return outcome;
    }

    /// <summary>
    /// True when the user holds OrganizationAdministrator in the organization
    /// and no other active member does. The role row is locked first (as
    /// revoking that role does), so concurrent removals can't take out the
    /// last two administrators together.
    /// </summary>
    private async Task<bool> IsLastOrganizationAdministratorAsync(int userId, int organizationId, CancellationToken cancellationToken)
    {
        var roleId = await roles.GetPlatformRoleIdAsync(DefaultRoleNames.OrganizationAdministrator, cancellationToken);
        if (!await roles.IsHeldByAsync(userId, roleId, organizationId, cancellationToken))
        {
            return false;
        }

        await roles.LockRoleAsync(roleId, cancellationToken);

        return await roles.CountActiveOrganizationHoldersAsync(roleId, organizationId, excludingUserId: userId, cancellationToken) == 0;
    }
}
