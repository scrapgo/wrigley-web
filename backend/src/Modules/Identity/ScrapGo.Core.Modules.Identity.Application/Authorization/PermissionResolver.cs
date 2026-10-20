using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Authorization;

/// <summary>
/// The single source of truth for what a user may do in one scope. It backs
/// the <c>[RequirePermission]</c> authorization handler, role and access
/// management, and the access reported on <c>GET /api/users/me</c>.
/// </summary>
/// <remarks>
/// <para>
/// Resolution is strictly per scope (<see cref="PermissionScope"/>): platform,
/// or one organization. No grant ever
/// satisfies a check in another scope. The scope is supplied by the caller
/// from the request context (route values), never from the token.
/// </para>
/// <para>
/// <b>Platform scope also needs a Google Workspace sign-in on this request</b>
/// (<see cref="ICallerSignIn.IsInternalWorkspaceSignIn"/>). Platform-scoped
/// grants are dormant for any session whose token lacks an allow-listed
/// <c>hd</c>, even for a PlatformAdministrator. The resolver answers for the
/// current caller, which is how every consumer uses it.
/// </para>
/// </remarks>
public sealed class PermissionResolver(
    IUserRepository users,
    IRoleRepository roles,
    IAuthorizationQueries queries,
    IPermissionCache cache,
    ICallerSignIn callerSignIn,
    TimeProvider timeProvider)
{
    /// <param name="organizationId">The organization to check in, or null for a platform-scoped check.</param>
    public async Task<bool> HasPermissionAsync(
        string identityPlatformUid, int? organizationId, string permissionName, CancellationToken cancellationToken) =>
        await users.GetIdByUidAsync(identityPlatformUid, cancellationToken) is { } userId
        && (await GetPermissionNamesAsync(new PermissionScope(userId, organizationId), cancellationToken))
            .Contains(permissionName);

    /// <summary>
    /// Every permission the user holds in exactly this scope, through the
    /// cache. Platform scope resolves to nothing without a Workspace sign-in on
    /// the current request, checked before the cache so a cached set can't
    /// bypass it.
    /// </summary>
    public async Task<IReadOnlySet<string>> GetPermissionNamesAsync(PermissionScope scope, CancellationToken cancellationToken)
    {
        if (scope.IsPlatform && !callerSignIn.IsInternalWorkspaceSignIn)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

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
    /// scope, the permissions <see cref="HasPermissionAsync"/> would grant, plus
    /// the organizations a client gates its UI on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mirrors enforcement exactly: an organization counts only with an active
    /// membership in an active organization (the membership guard denies
    /// everything else), and a grant only until it expires.
    /// </para>
    /// <para>
    /// Scopes with no permissions are left out of <see cref="EffectiveAccess.Permissions"/>.
    /// Every active organization is listed in <see cref="EffectiveAccess.Organizations"/>
    /// (the user is a member).
    /// </para>
    /// </remarks>
    public async Task<EffectiveAccess> GetEffectiveAccessAsync(int userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var organizations = await queries.ListActiveOrganizationsAsync(userId, cancellationToken);

        var activeOrganizationIds = organizations.Select(o => o.Id).ToHashSet();
        var platformSignIn = callerSignIn.IsInternalWorkspaceSignIn;
        var unexpiredRoles = (await roles.ListUserRolesAsync(userId, cancellationToken))
            .Where(r => r.ExpiresAt is null || r.ExpiresAt > now)
            .ToList();
        var workspaceSignInRequired = !platformSignIn && unexpiredRoles.Any(r => r.OrganizationId is null);
        var effectiveRoles = unexpiredRoles
            // Platform roles are dormant without a Workspace sign-in on this request.
            .Where(r => r.OrganizationId is not null || platformSignIn)
            .Where(r => r.OrganizationId is not { } organizationId || activeOrganizationIds.Contains(organizationId))
            .ToList();

        var scopes = new List<ScopedPermissionsDto>();
        if (effectiveRoles.Any(r => r.OrganizationId is null))
        {
            await AddScopeAsync(scopes, new PermissionScope(userId, null), cancellationToken);
        }

        var organizationAccess = new List<OrganizationAccessDto>();
        foreach (var organization in organizations)
        {
            var organizationPermissions = effectiveRoles.Any(r => r.OrganizationId == organization.Id)
                ? await AddScopeAsync(scopes, new PermissionScope(userId, organization.Id), cancellationToken)
                : [];

            organizationAccess.Add(new OrganizationAccessDto(organization.Id, organization.Name, organizationPermissions));
        }

        return new EffectiveAccess(effectiveRoles, scopes, organizationAccess, workspaceSignInRequired);
    }

    /// <summary>Resolves one scope, records it if non-empty, and returns its permission names ordered.</summary>
    private async Task<IReadOnlyList<string>> AddScopeAsync(
        List<ScopedPermissionsDto> scopes, PermissionScope scope, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> names = [.. (await GetPermissionNamesAsync(scope, cancellationToken)).Order(StringComparer.Ordinal)];
        if (names.Count > 0)
        {
            scopes.Add(new ScopedPermissionsDto(scope.OrganizationId, names));
        }

        return names;
    }
}

/// <param name="Roles">Platform roles first, then organization roles; expired grants left out.</param>
/// <param name="Permissions">One entry per scope the user holds something in.</param>
/// <param name="Organizations">The active organizations, with the permissions held in each, for UI gating.</param>
/// <param name="WorkspaceSignInRequired">
/// The user holds a platform role, but this sign-in isn't a Google Workspace one,
/// so it's left out of <paramref name="Roles"/> and <paramref name="Permissions"/>.
/// Only tells the UI why; it grants nothing.
/// </param>
public sealed record EffectiveAccess(
    IReadOnlyList<AssignedRoleDto> Roles,
    IReadOnlyList<ScopedPermissionsDto> Permissions,
    IReadOnlyList<OrganizationAccessDto> Organizations,
    bool WorkspaceSignInRequired);
