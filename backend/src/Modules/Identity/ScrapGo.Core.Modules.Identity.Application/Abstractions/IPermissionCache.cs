namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

/// <summary>
/// The scope a permission set is resolved in, for one user:
/// <list type="bullet">
/// <item>platform-wide when <see cref="OrganizationId"/> is null;</item>
/// <item>one organization when only <see cref="OrganizationId"/> is set;</item>
/// <item>one application in one organization when both are set: only that
/// application's grants, and only its enabled modules' permissions.</item>
/// </list>
/// </summary>
public readonly record struct PermissionScope(int UserId, int? OrganizationId, int? ApplicationId = null)
{
    public bool IsPlatform => OrganizationId is null;

    public bool IsApplication => ApplicationId is not null;
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

    /// <summary>
    /// Invalidates every cached scope inside one organization, for every user:
    /// used when the organization itself changes (deactivated, reactivated).
    /// </summary>
    /// <remarks>
    /// A real cache should implement this as a per-organization generation
    /// number that is part of every scope key, so one bump invalidates all
    /// holders in O(1) without enumerating them.
    /// </remarks>
    Task InvalidateOrganizationAsync(int organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates every cached (organization, application) scope for every
    /// user: used when the application is removed from the organization or a
    /// module is enabled or disabled. Same generation-number advice as
    /// <see cref="InvalidateOrganizationAsync"/>.
    /// </summary>
    Task InvalidateOrganizationApplicationAsync(int organizationId, int applicationId, CancellationToken cancellationToken);
}
