using System.Text.Json;
using ScrapGo.Core.Modules.Identity.Application.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Roles;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Applications;

/// <param name="ActorUid">The caller's UID from the validated token's <c>sub</c> claim.</param>
/// <param name="ActingAsPlatformAdmin">
/// True on the <c>/api/admin/…</c> routes: the caller is a platform
/// administrator acting from outside the organization. The escalation guard
/// and the last-application-administrator rule don't apply to them.
/// </param>
public sealed record ApplicationAccessCommand(
    string ActorUid, bool ActingAsPlatformAdmin, int OrganizationId, int ApplicationId, int UserId, int RoleId, DateTimeOffset? ExpiresAt = null);

public enum ApplicationGrantOutcome
{
    Granted,

    /// <summary>The user already held the role there with the same expiry. Nothing was written.</summary>
    AlreadyGranted,

    /// <summary>An expiry in the past.</summary>
    InvalidRequest,

    /// <summary>The organization doesn't have the application (or it's retired).</summary>
    ApplicationNotAssigned,

    UserNotFound,

    /// <summary>The target has no active membership in the organization.</summary>
    TargetNotMember,

    /// <summary>No such application role usable here (unknown, deleted, another application's, or another organization's).</summary>
    RoleNotFound,

    /// <summary>Escalation guard: the role grants a permission the caller doesn't hold in this application.</summary>
    CannotGrantUnheldPermission,
}

public enum ApplicationRevokeOutcome
{
    Revoked,

    /// <summary>The user didn't hold the role there. Nothing was written.</summary>
    NotHeld,

    UserNotFound,

    /// <summary>The user is the application's last active administrator in this organization.</summary>
    LastApplicationAdministrator,
}

public sealed record ApplicationGrantResult(ApplicationGrantOutcome Outcome, AssignedRoleDto? Grant = null);

/// <summary>
/// Access to one application within one organization: its roles (platform
/// templates plus the organization's custom ones), who holds them, and access
/// review.
/// </summary>
/// <remarks>
/// <para>
/// **Only application administrators or platform administrators grant
/// application access** (Decision 3). On the organization routes the caller
/// needs <c>Application.ManageAccess</c> at (organization, application) scope,
/// checked before this runs. Organization administrators can't grant
/// application roles. Platform administrators use the <c>/api/admin/…</c>
/// routes (<see cref="ApplicationAccessCommand.ActingAsPlatformAdmin"/>), for
/// example to appoint an application's first administrator.
/// </para>
/// <para>
/// The escalation guard holds as everywhere else: an application
/// administrator only grants, or composes into a custom role, permissions they
/// hold in that application, and never changes a role they hold. Every write
/// is one transaction with its audit row, and invalidates the affected
/// holders' cached permissions, so changes apply on their next request.
/// </para>
/// </remarks>
public sealed class ApplicationAccessService(
    IRoleRepository roles,
    IUserRepository users,
    IAuthorizationQueries queries,
    PermissionResolver permissionResolver,
    IPermissionCache permissionCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public Task<IReadOnlyList<RoleDetailDto>> ListRolesAsync(int organizationId, int applicationId, CancellationToken cancellationToken) =>
        queries.ListApplicationRolesAsync(organizationId, applicationId, cancellationToken);

    /// <summary>Everyone with access to the application in the organization, with what it resolves to now.</summary>
    public async Task<IReadOnlyList<AccessReviewEntryDto>> ReviewAccessAsync(
        int organizationId, int applicationId, CancellationToken cancellationToken)
    {
        var entries = new List<AccessReviewEntryDto>();
        foreach (var holder in (await queries.ListApplicationGrantHoldersAsync(organizationId, applicationId, cancellationToken)).GroupBy(h => h.UserId))
        {
            var permissions = await permissionResolver.GetPermissionNamesAsync(
                new PermissionScope(holder.Key, organizationId, applicationId), cancellationToken);
            entries.Add(new AccessReviewEntryDto(
                holder.Key,
                holder.First().Email,
                [.. holder.Select(h => h.Role)],
                [.. permissions.Order(StringComparer.Ordinal)]));
        }

        return entries;
    }

    public async Task<RoleMutationResult> CreateRoleAsync(
        string actorUid, int organizationId, int applicationId, string? name, string? description, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new(RoleMutationOutcome.InvalidRequest);
        }

        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);
        var role = Role.CreateForApplication(organizationId, applicationId, name, description, timeProvider.GetUtcNow());

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                roles.Add(role);
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.RoleCreated,
                    UserId: actorUserId,
                    OrganizationId: organizationId,
                    Metadata: JsonSerializer.Serialize(new { applicationId, name = role.Name, description = role.Description })));
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.RoleNamePerOrganization)
        {
            return new(RoleMutationOutcome.DuplicateName);
        }

        return new(RoleMutationOutcome.Success, RoleDto.From(role));
    }

    public async Task<RoleMutationResult> UpdateRoleAsync(
        string actorUid, int organizationId, int applicationId, int roleId, string? name, string? description, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new(RoleMutationOutcome.InvalidRequest);
        }

        if (await roles.FindEditableApplicationRoleAsync(roleId, organizationId, applicationId, cancellationToken) is not { } role)
        {
            return new(RoleMutationOutcome.NotFound);
        }

        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);
        if (actorUserId is { } actorId && await roles.IsHeldByAsync(actorId, role.Id, organizationId, cancellationToken))
        {
            return new(RoleMutationOutcome.CannotModifyOwnRole);
        }

        var before = new { name = role.Name, description = role.Description };
        role.Rename(name, description, timeProvider.GetUtcNow());

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.RoleUpdated,
                    UserId: actorUserId,
                    OrganizationId: organizationId,
                    Metadata: JsonSerializer.Serialize(new { applicationId, before, after = new { name = role.Name, description = role.Description } })));
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.RoleNamePerOrganization)
        {
            return new(RoleMutationOutcome.DuplicateName);
        }

        return new(RoleMutationOutcome.Success, RoleDto.From(role));
    }

    public async Task<RoleDeletionOutcome> DeleteRoleAsync(
        string actorUid, int organizationId, int applicationId, int roleId, CancellationToken cancellationToken)
    {
        if (await roles.FindEditableApplicationRoleAsync(roleId, organizationId, applicationId, cancellationToken) is not { } role)
        {
            return RoleDeletionOutcome.NotFound;
        }

        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);
        if (actorUserId is { } actorId && await roles.IsHeldByAsync(actorId, role.Id, organizationId, cancellationToken))
        {
            return RoleDeletionOutcome.CannotModifyOwnRole;
        }

        if (await roles.HasAssignmentsAsync(role.Id, cancellationToken))
        {
            return RoleDeletionOutcome.StillAssigned;
        }

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            role.MarkDeleted(timeProvider.GetUtcNow());
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.RoleDeleted,
                UserId: actorUserId,
                OrganizationId: organizationId,
                Metadata: JsonSerializer.Serialize(new { applicationId, roleId = role.Id, name = role.Name })));
            await unitOfWork.SaveChangesAsync(ct);
            return RoleDeletionOutcome.Success;
        }, cancellationToken);
    }

    /// <summary>Idempotent: attaching an already-attached permission succeeds without writing anything.</summary>
    public Task<RolePermissionOutcome> AttachPermissionAsync(
        string actorUid, int organizationId, int applicationId, int roleId, string? permissionName, CancellationToken cancellationToken) =>
        ChangePermissionAsync(actorUid, organizationId, applicationId, roleId, permissionName, attach: true, cancellationToken);

    /// <summary>Idempotent: detaching a permission the role doesn't have succeeds without writing anything.</summary>
    public Task<RolePermissionOutcome> DetachPermissionAsync(
        string actorUid, int organizationId, int applicationId, int roleId, string? permissionName, CancellationToken cancellationToken) =>
        ChangePermissionAsync(actorUid, organizationId, applicationId, roleId, permissionName, attach: false, cancellationToken);

    public async Task<ApplicationGrantResult> GrantAsync(ApplicationAccessCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (command.ExpiresAt is { } expiresAt && expiresAt <= now)
        {
            return new(ApplicationGrantOutcome.InvalidRequest);
        }

        if (!await queries.IsApplicationAvailableAsync(command.OrganizationId, command.ApplicationId, cancellationToken))
        {
            return new(ApplicationGrantOutcome.ApplicationNotAssigned);
        }

        if (await users.GetByIdAsync(command.UserId, cancellationToken) is null)
        {
            return new(ApplicationGrantOutcome.UserNotFound);
        }

        if (!await queries.HasActiveMembershipAsync(command.UserId, command.OrganizationId, cancellationToken))
        {
            return new(ApplicationGrantOutcome.TargetNotMember);
        }

        if (await roles.FindApplicationRoleAsync(command.RoleId, command.OrganizationId, command.ApplicationId, cancellationToken) is not { } role)
        {
            return new(ApplicationGrantOutcome.RoleNotFound);
        }

        var actorUserId = await users.GetIdByUidAsync(command.ActorUid, cancellationToken);
        if (!command.ActingAsPlatformAdmin)
        {
            // Escalation guard: never hand out more than you hold in this application.
            var held = actorUserId is { } actorId
                ? await permissionResolver.GetPermissionNamesAsync(
                    new PermissionScope(actorId, command.OrganizationId, command.ApplicationId), cancellationToken)
                : new HashSet<string>();
            var granted = await roles.GetRolePermissionsAsync(role.Id, cancellationToken);
            if (!granted.All(held.Contains))
            {
                return new(ApplicationGrantOutcome.CannotGrantUnheldPermission);
            }
        }

        var dto = new AssignedRoleDto(role.Id, role.Name, command.OrganizationId, command.ApplicationId, command.ExpiresAt);

        ApplicationGrantOutcome outcome;
        try
        {
            outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var updated = false;
                if (await roles.FindUserRoleAsync(command.UserId, role.Id, command.OrganizationId, ct) is { } existing)
                {
                    if (existing.ExpiresAt == command.ExpiresAt)
                    {
                        return ApplicationGrantOutcome.AlreadyGranted;
                    }

                    existing.ChangeExpiry(command.ExpiresAt, now);
                    updated = true;
                }
                else
                {
                    roles.AddUserRole(UserRole.AssignForApplication(
                        command.UserId, role.Id, command.OrganizationId, command.ApplicationId, command.ExpiresAt, now));
                }

                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.AccessGranted,
                    UserId: actorUserId,
                    OrganizationId: command.OrganizationId,
                    Metadata: JsonSerializer.Serialize(new
                    {
                        targetUserId = command.UserId,
                        roleId = role.Id,
                        organizationId = command.OrganizationId,
                        applicationId = command.ApplicationId,
                        expiresAt = command.ExpiresAt,
                        updated,
                        byPlatformAdmin = command.ActingAsPlatformAdmin,
                    })));
                await unitOfWork.SaveChangesAsync(ct);

                return ApplicationGrantOutcome.Granted;
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.OrganizationRoleAssignment)
        {
            // A concurrent request granted it first; its audit row stands, ours rolled back.
            return new(ApplicationGrantOutcome.AlreadyGranted, dto);
        }

        if (outcome == ApplicationGrantOutcome.Granted)
        {
            await permissionCache.InvalidateAsync(
                new PermissionScope(command.UserId, command.OrganizationId, command.ApplicationId), cancellationToken);
        }

        return new(outcome, dto);
    }

    public async Task<ApplicationRevokeOutcome> RevokeAsync(ApplicationAccessCommand command, CancellationToken cancellationToken)
    {
        if (await users.GetByIdAsync(command.UserId, cancellationToken) is null)
        {
            return ApplicationRevokeOutcome.UserNotFound;
        }

        var actorUserId = await users.GetIdByUidAsync(command.ActorUid, cancellationToken);

        var outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await roles.FindUserRoleAsync(command.UserId, command.RoleId, command.OrganizationId, ct) is not { } grant
                || grant.ApplicationId != command.ApplicationId)
            {
                return ApplicationRevokeOutcome.NotHeld;
            }

            if (!command.ActingAsPlatformAdmin
                && (await roles.GetRolePermissionsAsync(command.RoleId, ct)).Contains(Permissions.ApplicationManageAccess))
            {
                // Serialized like the other last-administrator checks: two
                // concurrent revocations can't each see "another one remains".
                await roles.LockRoleAsync(command.RoleId, ct);
                if (await roles.CountOtherApplicationAdministratorsAsync(
                        command.OrganizationId, command.ApplicationId, command.UserId, timeProvider.GetUtcNow(), ct) == 0)
                {
                    return ApplicationRevokeOutcome.LastApplicationAdministrator;
                }
            }

            roles.RemoveUserRole(grant);
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.AccessRevoked,
                UserId: actorUserId,
                OrganizationId: command.OrganizationId,
                Metadata: JsonSerializer.Serialize(new
                {
                    targetUserId = command.UserId,
                    roleId = command.RoleId,
                    organizationId = command.OrganizationId,
                    applicationId = command.ApplicationId,
                    byPlatformAdmin = command.ActingAsPlatformAdmin,
                })));
            await unitOfWork.SaveChangesAsync(ct);

            return ApplicationRevokeOutcome.Revoked;
        }, cancellationToken);

        if (outcome == ApplicationRevokeOutcome.Revoked)
        {
            await permissionCache.InvalidateAsync(
                new PermissionScope(command.UserId, command.OrganizationId, command.ApplicationId), cancellationToken);
        }

        return outcome;
    }

    private async Task<RolePermissionOutcome> ChangePermissionAsync(
        string actorUid, int organizationId, int applicationId, int roleId, string? permissionName, bool attach, CancellationToken cancellationToken)
    {
        if (await roles.FindEditableApplicationRoleAsync(roleId, organizationId, applicationId, cancellationToken) is not { } role)
        {
            return RolePermissionOutcome.RoleNotFound;
        }

        if (string.IsNullOrWhiteSpace(permissionName)
            || await roles.FindApplicationPermissionIdAsync(permissionName, applicationId, cancellationToken) is not { } permissionId)
        {
            return RolePermissionOutcome.UnknownPermission;
        }

        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);
        if (actorUserId is not { } actorId)
        {
            return RolePermissionOutcome.Forbidden;
        }

        if (await roles.IsHeldByAsync(actorId, role.Id, organizationId, cancellationToken))
        {
            return RolePermissionOutcome.CannotModifyOwnRole;
        }

        if (attach && !await permissionResolver.HasPermissionAsync(actorUid, organizationId, applicationId, permissionName, cancellationToken))
        {
            return RolePermissionOutcome.CannotGrantUnheldPermission;
        }

        var outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var existing = await roles.FindRolePermissionAsync(role.Id, permissionId, ct);
            switch (attach, existing)
            {
                case (true, null):
                    roles.AddRolePermission(RolePermission.Create(role.Id, permissionId));
                    break;
                case (false, not null):
                    roles.RemoveRolePermission(existing);
                    break;
                default:
                    return false;
            }

            auditLog.Record(new AuditEvent(
                attach ? IdentityAuditEventTypes.RolePermissionAttached : IdentityAuditEventTypes.RolePermissionDetached,
                UserId: actorUserId,
                OrganizationId: organizationId,
                Metadata: JsonSerializer.Serialize(new { applicationId, roleId = role.Id, permissionName })));
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        if (outcome)
        {
            foreach (var scope in await roles.GetAssignmentScopesAsync(role.Id, cancellationToken))
            {
                await permissionCache.InvalidateAsync(scope, cancellationToken);
            }
        }

        return RolePermissionOutcome.Success;
    }
}
