using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Authorization;

/// <summary>
/// The single source of truth for what a user may do. It backs the
/// <c>[RequirePermission]</c> authorization handler, role assignment, and the
/// access reported on <c>GET /api/users/me</c>.
/// </summary>
/// <remarks>
/// Roles are global, and every grant still needs a Google Workspace sign-in
/// on this request (<see cref="ICallerSignIn.IsInternalWorkspaceSignIn"/>):
/// grants are dormant for any session whose token lacks an allow-listed
/// <c>hd</c>, even for a PlatformAdministrator. The resolver answers for the
/// current caller, which is how every consumer uses it.
/// </remarks>
public sealed class PermissionResolver(
    IUserRepository users,
    IRoleRepository roles,
    IAuthorizationQueries queries,
    IPermissionCache cache,
    ICallerSignIn callerSignIn,
    TimeProvider timeProvider)
{
    public async Task<bool> HasPermissionAsync(string identityPlatformUid, string permissionName, CancellationToken cancellationToken) =>
        await users.GetIdByUidAsync(identityPlatformUid, cancellationToken) is { } userId
        && (await GetPermissionNamesAsync(userId, cancellationToken)).Contains(permissionName);

    /// <summary>
    /// Every permission the user holds, through the cache. Nothing resolves
    /// without a Workspace sign-in on the current request, checked before the
    /// cache so a cached set can't bypass it.
    /// </summary>
    public async Task<IReadOnlySet<string>> GetPermissionNamesAsync(int userId, CancellationToken cancellationToken)
    {
        if (!callerSignIn.IsInternalWorkspaceSignIn)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (await cache.GetAsync(userId, cancellationToken) is { } cached)
        {
            return cached;
        }

        var permissionNames = await queries.GetPermissionNamesAsync(userId, cancellationToken);
        await cache.SetAsync(userId, permissionNames, cancellationToken);

        return permissionNames;
    }

    /// <summary>
    /// What the user can actually do: their unexpired roles and the
    /// permissions <see cref="HasPermissionAsync"/> would grant. Both are empty
    /// without a Workspace sign-in.
    /// </summary>
    public async Task<EffectiveAccess> GetEffectiveAccessAsync(int userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var platformSignIn = callerSignIn.IsInternalWorkspaceSignIn;
        var unexpiredRoles = (await roles.ListUserRolesAsync(userId, cancellationToken))
            .Where(r => r.ExpiresAt is null || r.ExpiresAt > now)
            .ToList();

        if (!platformSignIn)
        {
            return new EffectiveAccess([], [], WorkspaceSignInRequired: unexpiredRoles.Count > 0);
        }

        IReadOnlyList<string> permissions = [.. (await GetPermissionNamesAsync(userId, cancellationToken)).Order(StringComparer.Ordinal)];
        return new EffectiveAccess(unexpiredRoles, permissions, WorkspaceSignInRequired: false);
    }
}

/// <param name="Roles">The unexpired roles the user holds, by name.</param>
/// <param name="Permissions">What those roles grant, ordered by name.</param>
/// <param name="WorkspaceSignInRequired">
/// The user holds a role, but this sign-in isn't a Google Workspace one, so
/// <paramref name="Roles"/> and <paramref name="Permissions"/> are empty.
/// Only tells the UI why; it grants nothing.
/// </param>
public sealed record EffectiveAccess(
    IReadOnlyList<AssignedRoleDto> Roles,
    IReadOnlyList<string> Permissions,
    bool WorkspaceSignInRequired);
