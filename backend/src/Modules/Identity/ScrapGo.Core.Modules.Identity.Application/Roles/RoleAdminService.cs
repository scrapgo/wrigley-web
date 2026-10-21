using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Roles;

/// <summary>
/// Role and permission administration (ACCESS-MODEL.md section 9). V1 has a
/// fixed set of roles: admins change which permissions a role grants, never
/// create or delete roles. Reads need <c>roles.view</c> and changes
/// <c>roles.manage</c>, checked on the routes before these methods run.
/// </summary>
/// <remarks>
/// The audience rule (section 5): a permission with audience Internal maps
/// only to internal roles, External only to external roles, Any to both. A
/// change commits with its <c>role.permissions_changed</c> audit row, then
/// evicts every cached access context, since any number of users may hold the role.
/// </remarks>
public sealed class RoleAdminService(
    IRoleRepository roles,
    ICurrentAccess currentAccess,
    IAccessCache accessCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog)
{
    public Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken) =>
        roles.ListRolesAsync(cancellationToken);

    public Task<IReadOnlyList<PermissionGroupDto>> ListPermissionsAsync(CancellationToken cancellationToken) =>
        roles.ListPermissionGroupsAsync(cancellationToken);

    /// <summary>Replaces the role's permissions with exactly <see cref="SetRolePermissionsCommand.Permissions"/>.</summary>
    public async Task<RoleAdminResult> SetPermissionsAsync(SetRolePermissionsCommand command, CancellationToken cancellationToken)
    {
        if (command.Permissions is not { } requested || requested.Any(string.IsNullOrWhiteSpace))
        {
            return new(RoleAdminOutcome.InvalidRequest);
        }

        if (await roles.FindActiveRoleAsync(command.RoleId, cancellationToken) is not { } role)
        {
            return new(RoleAdminOutcome.NotFound);
        }

        var keys = requested.Select(key => key.Trim()).ToHashSet(StringComparer.Ordinal);
        var wanted = await roles.FindPermissionsByKeysAsync(keys, cancellationToken);
        if (wanted.Count != keys.Count)
        {
            return new(RoleAdminOutcome.UnknownPermission);
        }

        if (wanted.Any(permission => !FitsAudience(permission.Audience, role.UserType)))
        {
            return new(RoleAdminOutcome.AudienceMismatch);
        }

        var actorId = (await currentAccess.GetAsync(cancellationToken))?.UserId
            ?? throw new InvalidOperationException("An authorized role-admin request has no access context.");

        var changed = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var current = await roles.ListRolePermissionsAsync(role.Id, ct);
            var toRemove = current.Where(grant => !keys.Contains(grant.Permission.Key)).ToList();
            var toAdd = wanted.Where(permission => current.All(grant => grant.PermissionId != permission.Id)).ToList();
            if (toRemove.Count == 0 && toAdd.Count == 0)
            {
                return false;
            }

            foreach (var grant in toRemove)
            {
                roles.RemoveRolePermission(grant);
            }

            foreach (var permission in toAdd)
            {
                roles.AddRolePermission(RolePermission.Create(role.Id, permission.Id));
            }

            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.RolePermissionsChanged,
                UserId: actorId,
                Metadata: JsonSerializer.Serialize(new
                {
                    roleId = role.Id,
                    roleKey = role.Key,
                    added = toAdd.Select(permission => permission.Key).Order(StringComparer.Ordinal),
                    removed = toRemove.Select(grant => grant.Permission.Key).Order(StringComparer.Ordinal),
                })));
            await unitOfWork.SaveChangesAsync(ct);

            return true;
        }, cancellationToken);

        if (changed)
        {
            accessCache.EvictAll();
        }

        return new(RoleAdminOutcome.Success, await roles.GetRoleAsync(role.Id, cancellationToken));
    }

    private static bool FitsAudience(PermissionAudience audience, UserType roleType) => audience switch
    {
        PermissionAudience.Any => true,
        PermissionAudience.Internal => roleType == UserType.Internal,
        PermissionAudience.External => roleType == UserType.External,
        _ => false,
    };
}
