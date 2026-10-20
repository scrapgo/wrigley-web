namespace ScrapGo.Core.Modules.Identity.Application.Access;

/// <summary>
/// Caches access contexts by UID for a short time (30 seconds). Other Cloud
/// Run instances catch up within that window, which is the agreed revocation
/// target (ACCESS-MODEL.md section 8).
/// </summary>
/// <remarks>
/// Admin changes evict: a change to one user calls <see cref="EvictUser"/>; a
/// role-permission or feature change calls <see cref="EvictAll"/>.
/// </remarks>
public interface IAccessCache
{
    /// <summary>The cached context for the UID, or the result of <paramref name="load"/>, cached. Unknown users (null) aren't cached.</summary>
    Task<AccessContext?> GetOrLoadAsync(
        string identityPlatformUid, Func<CancellationToken, Task<AccessContext?>> load, CancellationToken cancellationToken);

    void EvictUser(int userId);

    void EvictAll();
}
