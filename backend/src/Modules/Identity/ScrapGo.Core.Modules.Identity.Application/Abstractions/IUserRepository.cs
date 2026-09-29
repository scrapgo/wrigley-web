namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

public interface IUserRepository
{
    /// <summary>
    /// Returns the user for <paramref name="identityPlatformUid"/>, inserting
    /// <paramref name="candidate"/> first if no row exists yet. Implementations
    /// must be exactly-once under concurrency through a real DB-level
    /// idempotent insert, not a read-then-write check.
    /// </summary>
    Task<ProvisionedUser> InsertIfAbsentAsync(User candidate, CancellationToken cancellationToken);

    /// <summary>A lightweight projection, because the status gate runs on every authenticated request.</summary>
    Task<UserStatusSnapshot?> GetStatusByUidAsync(string identityPlatformUid, CancellationToken cancellationToken);

    Task<int?> GetIdByUidAsync(string identityPlatformUid, CancellationToken cancellationToken);
}

/// <param name="WasCreated">True only when this call inserted the row (first sighting of the UID).</param>
public sealed record ProvisionedUser(User User, bool WasCreated);

public sealed record UserStatusSnapshot(int UserId, UserStatus Status);
