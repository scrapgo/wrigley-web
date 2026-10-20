using ScrapGo.Core.Modules.Identity.Application.Access;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// <see cref="ICurrentAccess"/> for an HTTP request: the caller is the token's
/// <c>sub</c>. Request-scoped, so the context is loaded (through the cache) at
/// most once per request, however many checks ask for it.
/// </summary>
public sealed class HttpCurrentAccess(
    IHttpContextAccessor httpContextAccessor, IAccessCache cache, IAccessContextProvider provider) : ICurrentAccess
{
    private Task<AccessContext?>? _current;

    public Task<AccessContext?> GetAsync(CancellationToken cancellationToken) =>
        _current ??= LoadAsync(cancellationToken);

    private Task<AccessContext?> LoadAsync(CancellationToken cancellationToken) =>
        httpContextAccessor.HttpContext?.User.GetIdentityPlatformUid() is { } uid
            ? cache.GetOrLoadAsync(uid, ct => provider.LoadAsync(uid, ct), cancellationToken)
            : Task.FromResult<AccessContext?>(null);
}
