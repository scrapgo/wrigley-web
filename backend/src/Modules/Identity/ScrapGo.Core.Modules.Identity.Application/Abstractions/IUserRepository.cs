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

    /// <summary>A tracked user, or null. For mutations; list reads go through <see cref="IAuthorizationQueries.ListUsersAsync"/>.</summary>
    Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies <see cref="User.Enable"/> or <see cref="User.Disable"/> to the
    /// tracked user and returns their status <em>before</em> the change (for
    /// audit and idempotency), or null if there is no such user. Nothing is
    /// written until the caller saves through <see cref="IUnitOfWork"/>.
    /// </summary>
    Task<UserStatus?> UpdateStatusAsync(int userId, UserStatus status, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <param name="WasCreated">True only when this call inserted the row (first sighting of the UID).</param>
public sealed record ProvisionedUser(User User, bool WasCreated);

public sealed record UserStatusSnapshot(int UserId, UserStatus Status);
