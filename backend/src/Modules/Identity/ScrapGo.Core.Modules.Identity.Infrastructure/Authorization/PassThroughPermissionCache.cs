namespace ScrapGo.Core.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// An <see cref="IPermissionCache"/> that caches nothing: every lookup misses,
/// so permissions always resolve from Postgres. It stands in until the Redis
/// (Memorystore) cache is migrated. The handlers already make every
/// invalidation call, so swapping in a real cache is a registration change only.
/// </summary>
public sealed class PassThroughPermissionCache : IPermissionCache
{
    public Task<IReadOnlySet<string>?> GetAsync(PermissionScope scope, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>?>(null);

    public Task SetAsync(PermissionScope scope, IReadOnlySet<string> permissionNames, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task InvalidateAsync(PermissionScope scope, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task InvalidateOrganizationAsync(int organizationId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task InvalidateOrganizationApplicationAsync(int organizationId, int applicationId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
