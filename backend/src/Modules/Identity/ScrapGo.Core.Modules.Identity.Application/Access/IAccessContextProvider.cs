namespace ScrapGo.Core.Modules.Identity.Application.Access;

/// <summary>
/// Builds a user's <see cref="AccessContext"/> from the database, uncached.
/// Callers normally go through <see cref="IAccessCache"/> or <see cref="ICurrentAccess"/>.
/// </summary>
public interface IAccessContextProvider
{
    /// <summary>The access context of the user with this UID, or null when there is no such user.</summary>
    Task<AccessContext?> LoadAsync(string identityPlatformUid, CancellationToken cancellationToken);
}
