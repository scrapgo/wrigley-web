using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Shared.Kernel.Paging;

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

    /// <summary>Stages supplier links for an External user; written by the next save.</summary>
    void AddSupplierLinks(IEnumerable<UserSupplier> links);

    /// <summary>Stages removal of links found by <see cref="ListSupplierLinksAsync"/>.</summary>
    void RemoveSupplierLinks(IEnumerable<UserSupplier> links);

    /// <summary>The user's supplier links, tracked, for changing them.</summary>
    Task<IReadOnlyList<UserSupplier>> ListSupplierLinksAsync(int userId, CancellationToken cancellationToken);

    /// <summary>The user's linked supplier Record IDs, ascending.</summary>
    Task<IReadOnlyList<int>> ListSupplierRecordIdsAsync(int userId, CancellationToken cancellationToken);

    Task<int?> GetIdByUidAsync(string identityPlatformUid, CancellationToken cancellationToken);

    /// <summary>The emails of the users with these ids; ids with no user are missing.</summary>
    Task<IReadOnlyDictionary<int, string>> GetEmailsAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken);

    /// <summary>A tracked user, or null. For mutations; list reads go through <see cref="ListAsync"/>.</summary>
    Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Every user, filtered and paged by id. Assumes a valid <see cref="PageRequest"/>.</summary>
    Task<PagedResult<UserSummaryDto>> ListAsync(UserListFilter filter, PageRequest page, CancellationToken cancellationToken);

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

