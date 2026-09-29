namespace ScrapGo.Core.Modules.Identity.Application.Authorization;

/// <summary>
/// Decides whether a caller holds a permission in one scope. It backs the
/// <c>[RequirePermission]</c> authorization handler.
/// </summary>
/// <remarks>
/// Resolution is strictly per scope. An organization-scoped check sees only
/// the caller's roles in that organization. A platform-scoped check sees
/// only user roles with no organization, so no organization-level grant can
/// satisfy it. The scope is supplied by the caller from the request context
/// (route values), never from the token.
/// </remarks>
public sealed class PermissionResolver(IUserRepository users, IAuthorizationQueries queries, IPermissionCache cache)
{
    /// <param name="organizationId">The organization to check in, or null for a platform-scoped check.</param>
    public async Task<bool> HasPermissionAsync(
        string identityPlatformUid, int? organizationId, string permissionName, CancellationToken cancellationToken)
    {
        if (await users.GetIdByUidAsync(identityPlatformUid, cancellationToken) is not { } userId)
        {
            return false;
        }

        var scope = new PermissionScope(userId, organizationId);

        var permissionNames = await cache.GetAsync(scope, cancellationToken);
        if (permissionNames is null)
        {
            permissionNames = await queries.GetPermissionNamesAsync(scope, cancellationToken);
            await cache.SetAsync(scope, permissionNames, cancellationToken);
        }

        return permissionNames.Contains(permissionName);
    }
}
