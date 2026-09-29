namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

/// <summary>
/// The scope a permission set is resolved in: one user, in one organization,
/// or platform-wide when <see cref="OrganizationId"/> is null.
/// </summary>
public readonly record struct PermissionScope(int UserId, int? OrganizationId)
{
    public bool IsPlatform => OrganizationId is null;
}

/// <summary>
/// Caches resolved permission sets per <see cref="PermissionScope"/>.
/// Implementations must fail open to the database: a cache outage degrades
/// every lookup to "resolve from Postgres", and never grants or denies on
/// its own.
/// </summary>
/// <remarks>
/// Every mutation that changes what a scope resolves to must call
/// <see cref="InvalidateAsync"/>. Role edits, deletes and permission
/// attach/detach already do (see <c>RoleService</c>).
/// </remarks>
public interface IPermissionCache
{
    /// <summary>The cached set, or null on a miss (or when the cache is unavailable).</summary>
    Task<IReadOnlySet<string>?> GetAsync(PermissionScope scope, CancellationToken cancellationToken);

    Task SetAsync(PermissionScope scope, IReadOnlySet<string> permissionNames, CancellationToken cancellationToken);

    Task InvalidateAsync(PermissionScope scope, CancellationToken cancellationToken);
}
