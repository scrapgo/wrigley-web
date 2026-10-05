using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Authorization;

/// <summary>
/// The single source of truth for what a user may do in one scope. It backs
/// the <c>[RequirePermission]</c> authorization handler, role management, and
/// the permissions reported on <c>GET /api/users/me</c>.
/// </summary>
/// <remarks>
/// Resolution is strictly per scope. An organization-scoped check sees only
/// the caller's roles in that organization. A platform-scoped check sees
/// only user roles with no organization, so no organization-level grant can
/// satisfy it. The scope is supplied by the caller from the request context
/// (route values), never from the token.
/// </remarks>
public sealed class PermissionResolver(
    IUserRepository users,
    IRoleRepository roles,
    IAuthorizationQueries queries,
    IPermissionCache cache)
{
    /// <param name="organizationId">The organization to check in, or null for a platform-scoped check.</param>
    public async Task<bool> HasPermissionAsync(
        string identityPlatformUid, int? organizationId, string permissionName, CancellationToken cancellationToken) =>
        await users.GetIdByUidAsync(identityPlatformUid, cancellationToken) is { } userId
        && (await GetPermissionNamesAsync(new PermissionScope(userId, organizationId), cancellationToken)).Contains(permissionName);

    /// <summary>Every permission the user holds in exactly this scope, through the cache.</summary>
    public async Task<IReadOnlySet<string>> GetPermissionNamesAsync(PermissionScope scope, CancellationToken cancellationToken)
    {
        if (await cache.GetAsync(scope, cancellationToken) is { } cached)
        {
            return cached;
        }

        var permissionNames = await queries.GetPermissionNamesAsync(scope, cancellationToken);
        await cache.SetAsync(scope, permissionNames, cancellationToken);

        return permissionNames;
    }

    /// <summary>
    /// What the user can actually do, scope by scope: their roles and, per
    /// scope, the permissions <see cref="HasPermissionAsync"/> would grant.
    /// </summary>
    /// <remarks>
    /// Organization scopes count only where the user has an active membership,
    /// because the membership guard denies every organization-scoped route
    /// anywhere else, whatever roles the user still holds there. Scopes with no
    /// permissions are left out. Platform scope comes first, then organizations by id.
    /// </remarks>
    public async Task<EffectiveAccess> GetEffectiveAccessAsync(int userId, CancellationToken cancellationToken)
    {
        var effectiveRoles = new List<AssignedRoleDto>();
        foreach (var role in await roles.ListUserRolesAsync(userId, cancellationToken))
        {
            if (role.OrganizationId is not { } organizationId
                || await queries.HasActiveMembershipAsync(userId, organizationId, cancellationToken))
            {
                effectiveRoles.Add(role);
            }
        }

        var scopes = new List<ScopedPermissionsDto>();
        foreach (var organizationId in effectiveRoles.Select(r => r.OrganizationId).Distinct())
        {
            var names = await GetPermissionNamesAsync(new PermissionScope(userId, organizationId), cancellationToken);
            if (names.Count > 0)
            {
                scopes.Add(new ScopedPermissionsDto(organizationId, [.. names.Order(StringComparer.Ordinal)]));
            }
        }

        return new EffectiveAccess(effectiveRoles, scopes);
    }
}

/// <param name="Roles">Platform-scoped roles first, then organization roles by organization id and name.</param>
public sealed record EffectiveAccess(IReadOnlyList<AssignedRoleDto> Roles, IReadOnlyList<ScopedPermissionsDto> Permissions);
