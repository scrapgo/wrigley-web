namespace ScrapGo.Core.Modules.Identity.Application.Authorization;

/// <summary>The read-only permission catalog. There is deliberately no write path.</summary>
public sealed class ListPermissionsHandler(IRoleRepository roles)
{
    public Task<IReadOnlyList<string>> HandleAsync(CancellationToken cancellationToken) =>
        roles.ListPermissionKeysAsync(cancellationToken);
}
