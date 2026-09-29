using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Roles;

/// <summary>
/// Custom, organization-scoped roles: create, edit, soft-delete, and compose
/// from the permission catalog.
/// </summary>
/// <remarks>
/// <para>
/// Every operation is organization-scoped and re-authorized server-side. The
/// caller must be an OrganizationAdministrator, with an active membership,
/// of the role's own organization. For create, that organization is the one
/// named in the request. For every other operation it is the organization
/// the stored role belongs to, never anything the client sends. A caller who
/// can't rename a role can't change what it grants either.
/// </para>
/// <para>
/// Built-in platform-scoped roles (e.g. OrganizationAdministrator) and
/// soft-deleted roles are not editable here: they resolve as NotFound.
/// Every successful mutation is audit-logged in the same transaction and
/// invalidates the cached permission sets of every current holder of the role.
/// </para>
/// </remarks>
public sealed class RoleService(
    IRoleRepository roles,
    IUserRepository users,
    IAuthorizationQueries authorization,
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

        if (await ResolveAdministratorAsync(command.ActorUid, organizationId, cancellationToken) is not { } actorUserId)
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

        if (await ResolveAdministratorAsync(command.ActorUid, role.OrganizationId!.Value, cancellationToken) is not { } actorUserId)
        {
            return new(RoleMutationOutcome.Forbidden);
        }

        var before = new { name = role.Name, description = role.Description };
        role.Rename(command.Name, command.Description, timeProvider.GetUtcNow());

        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.RoleUpdated,
            UserId: actorUserId,
            OrganizationId: role.OrganizationId,
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

        if (await ResolveAdministratorAsync(command.ActorUid, role.OrganizationId!.Value, cancellationToken) is not { } actorUserId)
        {
            return RoleDeletionOutcome.Forbidden;
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
            OrganizationId: role.OrganizationId,
            Metadata: JsonSerializer.Serialize(new { roleId = role.Id, name = role.Name })));

        await unitOfWork.SaveChangesAsync(cancellationToken);
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

        if (await ResolveAdministratorAsync(command.ActorUid, role.OrganizationId!.Value, cancellationToken) is not { } actorUserId)
        {
            return RolePermissionOutcome.Forbidden;
        }

        if (string.IsNullOrWhiteSpace(command.PermissionName)
            || await roles.FindPermissionIdAsync(command.PermissionName, cancellationToken) is not { } permissionId)
        {
            return RolePermissionOutcome.UnknownPermission;
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
            OrganizationId: role.OrganizationId,
            Metadata: JsonSerializer.Serialize(new { roleId = role.Id, permissionName = command.PermissionName })));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateHoldersAsync(role.Id, cancellationToken);

        return RolePermissionOutcome.Success;
    }

    /// <summary>
    /// The actor's user id if they administer <paramref name="organizationId"/>
    /// (active membership plus the OrganizationAdministrator role there),
    /// otherwise null.
    /// </summary>
    private async Task<int?> ResolveAdministratorAsync(string actorUid, int organizationId, CancellationToken cancellationToken) =>
        await users.GetIdByUidAsync(actorUid, cancellationToken) is { } userId
        && await authorization.IsOrganizationAdministratorAsync(userId, organizationId, cancellationToken)
            ? userId
            : null;

    /// <summary>False when the save lost to the per-organization unique role name.</summary>
    private async Task<bool> TrySaveRoleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.RoleNamePerOrganization)
        {
            return false;
        }
    }

    private async Task InvalidateHoldersAsync(int roleId, CancellationToken cancellationToken)
    {
        foreach (var scope in await roles.GetAssignmentScopesAsync(roleId, cancellationToken))
        {
            await permissionCache.InvalidateAsync(scope, cancellationToken);
        }
    }
}
