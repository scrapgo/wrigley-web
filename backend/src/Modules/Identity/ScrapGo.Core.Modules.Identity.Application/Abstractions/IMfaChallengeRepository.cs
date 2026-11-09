namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

public interface IMfaChallengeRepository
{
    void Add(MfaChallenge challenge);

    /// <summary>The challenge for this user's sign-in, tracked, or null.</summary>
    Task<MfaChallenge?> FindAsync(int userId, DateTimeOffset authTime, CancellationToken cancellationToken);

    /// <summary>Whether this user's sign-in has a verified code. Read-only, for the per-request gate.</summary>
    Task<bool> IsVerifiedAsync(int userId, DateTimeOffset authTime, CancellationToken cancellationToken);
}
