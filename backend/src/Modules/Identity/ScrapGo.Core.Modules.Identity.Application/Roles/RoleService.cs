using System.Text.Json;
using ScrapGo.Core.Modules.Identity.Application.Authorization;

namespace ScrapGo.Core.Modules.Identity.Application.Roles;

/// <summary>
/// Custom, organization-scoped roles: create, edit, soft-delete, and compose
/// from the permission catalog.
/// </summary>
/// <remarks>
/// <para>
/// Every operation is organization-scoped, permission-based and re-authorized
/// server-side. The caller needs an active membership in the role's
/// organization, plus the permission for the operation there: <c>Role.Create</c>,
/// <c>Role.Update</c> (edit, and attach or detach permissions) or
/// <c>Role.Delete</c>. For create, the organization is the one named in the
/// request. For every other operation it is the organization the stored role
/// belongs to, never anything the client sends. No role name is consulted:
/// whoever holds the permission there, through any role, may act.
/// </para>
/// <para>
/// Escalation guard. A caller may only attach a permission they already hold
/// in that organization, and may not change a role they hold themselves.
/// Without it, a <c>Role.Update</c> holder could grant themselves anything.
/// </para>
/// <para>
/// Built-in platform-defined roles (OrganizationAdministrator,
/// PlatformAdministrator) and soft-deleted roles are not editable here: they
/// resolve as NotFound. Every successful mutation is audit-logged in the same
/// transaction and invalidates the cached permission sets of every current
/// holder of the role.
/// </para>
/// </remarks>
public sealed class RoleService(
    IRoleRepository roles,
    IUserRepository users,
    IAuthorizationQueries authorization,
    PermissionResolver permissionResolver,
    IPermissionCache permissionCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public async Task<RoleMutationResult> CreateAsync(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        if (command.OrganizationId is not { } organizationId || string.IsNullOrWhiteSpace(command.Name))
        {
            return new(RoleMutationOutcome.InvalidRequest);
        }

        if (await AuthorizeAsync(command.ActorUid, organizationId, Permissions.RoleCreate, cancellationToken) is not { } actorUserId)
        {
            return new(RoleMutationOutcome.Forbidden);
        }

        var role = Role.CreateForOrganization(organizationId, command.Name, command.Description, timeProvider.GetUtcNow());
        roles.Add(role);

        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.RoleCreated,
            UserId: actorUserId,
            OrganizationId: organizationId,
            Metadata: JsonSerializer.Serialize(new { name = role.Name, description = role.Description })));

        if (!await TrySaveRoleAsync(cancellationToken))
        {
            return new(RoleMutationOutcome.DuplicateName);
        }

        return new(RoleMutationOutcome.Success, RoleDto.From(role));
    }

    public async Task<RoleMutationResult> UpdateAsync(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new(RoleMutationOutcome.InvalidRequest);
        }

        if (await roles.FindEditableOrganizationRoleAsync(command.RoleId, cancellationToken) is not { } role)
        {
            return new(RoleMutationOutcome.NotFound);
        }

        var organizationId = role.OrganizationId!.Value;
        if (await AuthorizeAsync(command.ActorUid, organizationId, Permissions.RoleUpdate, cancellationToken) is not { } actorUserId)
        {
            return new(RoleMutationOutcome.Forbidden);
        }

        if (await roles.IsHeldByAsync(actorUserId, role.Id, organizationId, cancellationToken))
        {
            return new(RoleMutationOutcome.CannotModifyOwnRole);
        }

        var before = new { name = role.Name, description = role.Description };
        role.Rename(command.Name, command.Description, timeProvider.GetUtcNow());

        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.RoleUpdated,
            UserId: actorUserId,
            OrganizationId: organizationId,
            Metadata: JsonSerializer.Serialize(new { before, after = new { name = role.Name, description = role.Description } })));

        if (!await TrySaveRoleAsync(cancellationToken))
        {
            return new(RoleMutationOutcome.DuplicateName);
        }

        await InvalidateHoldersAsync(role.Id, cancellationToken);

        return new(RoleMutationOutcome.Success, RoleDto.From(role));
    }

    public async Task<RoleDeletionOutcome> DeleteAsync(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        if (await roles.FindEditableOrganizationRoleAsync(command.RoleId, cancellationToken) is not { } role)
        {
            return RoleDeletionOutcome.NotFound;
        }

        var organizationId = role.OrganizationId!.Value;
        if (await AuthorizeAsync(command.ActorUid, organizationId, Permissions.RoleDelete, cancellationToken) is not { } actorUserId)
        {
            return RoleDeletionOutcome.Forbidden;
        }

        if (await roles.IsHeldByAsync(actorUserId, role.Id, organizationId, cancellationToken))
        {
            return RoleDeletionOutcome.CannotModifyOwnRole;
        }

        // No cascading revocation: every assignment must be revoked explicitly first.
        if (await roles.HasAssignmentsAsync(role.Id, cancellationToken))
        {
            return RoleDeletionOutcome.StillAssigned;
        }

        role.MarkDeleted(timeProvider.GetUtcNow());

        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.RoleDeleted,
            UserId: actorUserId,
            OrganizationId: organizationId,
            Metadata: JsonSerializer.Serialize(new { roleId = role.Id, name = role.Name })));

        await CommitAsync(cancellationToken);
        await InvalidateHoldersAsync(role.Id, cancellationToken);

        return RoleDeletionOutcome.Success;
    }

    /// <summary>Idempotent: attaching an already-attached permission succeeds without writing anything.</summary>
    public Task<RolePermissionOutcome> AttachPermissionAsync(RolePermissionCommand command, CancellationToken cancellationToken) =>
        ChangePermissionAsync(command, attach: true, cancellationToken);

    /// <summary>Idempotent: detaching a permission the role doesn't have succeeds without writing anything.</summary>
    public Task<RolePermissionOutcome> DetachPermissionAsync(RolePermissionCommand command, CancellationToken cancellationToken) =>
        ChangePermissionAsync(command, attach: false, cancellationToken);

    private async Task<RolePermissionOutcome> ChangePermissionAsync(
        RolePermissionCommand command, bool attach, CancellationToken cancellationToken)
    {
        if (await roles.FindEditableOrganizationRoleAsync(command.RoleId, cancellationToken) is not { } role)
        {
            return RolePermissionOutcome.RoleNotFound;
        }

        // Composing a role is editing it, so attach and detach both need Role.Update.
        // Role.Assign is reserved for assigning roles to users.
        var organizationId = role.OrganizationId!.Value;
        if (await AuthorizeAsync(command.ActorUid, organizationId, Permissions.RoleUpdate, cancellationToken) is not { } actorUserId)
        {
            return RolePermissionOutcome.Forbidden;
        }

        if (string.IsNullOrWhiteSpace(command.PermissionName)
            || await roles.FindPermissionIdAsync(command.PermissionName, cancellationToken) is not { } permissionId)
        {
            return RolePermissionOutcome.UnknownPermission;
        }

        if (await roles.IsHeldByAsync(actorUserId, role.Id, organizationId, cancellationToken))
        {
            return RolePermissionOutcome.CannotModifyOwnRole;
        }

        // Only attaching can escalate; detaching only ever removes access.
        if (attach && !await permissionResolver.HasPermissionAsync(command.ActorUid, organizationId, command.PermissionName, cancellationToken))
        {
            return RolePermissionOutcome.CannotGrantUnheldPermission;
        }

        var existing = await roles.FindRolePermissionAsync(role.Id, permissionId, cancellationToken);

        switch (attach, existing)
        {
            case (true, null):
                roles.AddRolePermission(RolePermission.Create(role.Id, permissionId));
                break;
            case (false, not null):
                roles.RemoveRolePermission(existing);
                break;
            default:
                return RolePermissionOutcome.Success;
        }

        auditLog.Record(new AuditEvent(
            attach ? IdentityAuditEventTypes.RolePermissionAttached : IdentityAuditEventTypes.RolePermissionDetached,
            UserId: actorUserId,
            OrganizationId: organizationId,
            Metadata: JsonSerializer.Serialize(new { roleId = role.Id, permissionName = command.PermissionName })));

        await CommitAsync(cancellationToken);
        await InvalidateHoldersAsync(role.Id, cancellationToken);

        return RolePermissionOutcome.Success;
    }

    /// <summary>
    /// The actor's user id when they have an active membership in
    /// <paramref name="organizationId"/> and hold <paramref name="permissionName"/>
    /// there, otherwise null.
    /// </summary>
    /// <remarks>
    /// The membership check is explicit because these routes carry no
    /// <c>{organizationId}</c>, so the membership guard middleware never runs
    /// for them, and <see cref="PermissionResolver"/> doesn't check membership.
    /// </remarks>
    private async Task<int?> AuthorizeAsync(
        string actorUid, int organizationId, string permissionName, CancellationToken cancellationToken) =>
        await users.GetIdByUidAsync(actorUid, cancellationToken) is { } userId
        && await authorization.HasActiveMembershipAsync(userId, organizationId, cancellationToken)
        && await permissionResolver.HasPermissionAsync(actorUid, organizationId, permissionName, cancellationToken)
            ? userId
            : null;

    /// <summary>False when the save lost to the per-organization unique role name.</summary>
    private async Task<bool> TrySaveRoleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CommitAsync(cancellationToken);
            return true;
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.RoleNamePerOrganization)
        {
            return false;
        }
    }

    /// <summary>
    /// Commits the staged change and its staged audit row together, through the
    /// module's transaction boundary like every other Identity write.
    /// </summary>
    private Task CommitAsync(CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    private async Task InvalidateHoldersAsync(int roleId, CancellationToken cancellationToken)
    {
        foreach (var scope in await roles.GetAssignmentScopesAsync(roleId, cancellationToken))
        {
            await permissionCache.InvalidateAsync(scope, cancellationToken);
        }
    }
}
