namespace ScrapGo.Core.Modules.Identity.Application.Access;

/// <summary>
/// The calling user's <see cref="AccessContext"/>, loaded at most once per
/// request (through <see cref="IAccessCache"/>). Request-scoped.
/// </summary>
public interface ICurrentAccess
{
    /// <summary>The caller's access context, or null for an unauthenticated caller or an unknown UID.</summary>
    Task<AccessContext?> GetAsync(CancellationToken cancellationToken);
}
