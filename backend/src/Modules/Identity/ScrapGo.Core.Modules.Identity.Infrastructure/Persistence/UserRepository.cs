using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public void AddSupplierLinks(IEnumerable<UserSupplier> links) => dbContext.UserSuppliers.AddRange(links);

    public void RemoveSupplierLinks(IEnumerable<UserSupplier> links) => dbContext.UserSuppliers.RemoveRange(links);

    public async Task<IReadOnlyList<UserSupplier>> ListSupplierLinksAsync(int userId, CancellationToken cancellationToken) =>
        await dbContext.UserSuppliers.Where(s => s.UserId == userId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> ListSupplierRecordIdsAsync(int userId, CancellationToken cancellationToken) =>
        await dbContext.UserSuppliers
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.SupplierRecordId)
            .Select(s => s.SupplierRecordId)
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<UserSummaryDto>> ListAsync(UserListFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var users = dbContext.Users.AsNoTracking();

        if (filter.Status is { } status)
        {
            users = users.Where(u => u.Status == status);
        }

        if (filter.UserType is { } userType)
        {
            users = users.Where(u => u.UserType == userType);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = LikePatterns.Contains(filter.Search.Trim());
            users = users.Where(u =>
                EF.Functions.ILike(u.Email, pattern, LikePatterns.EscapeCharacter)
                || (u.DisplayName != null && EF.Functions.ILike(u.DisplayName, pattern, LikePatterns.EscapeCharacter)));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var rows = await users
            .OrderBy(u => u.Id)
            .Skip(page.Skip)
            .Take(page.ResolvedPageSize)
            .Select(u => new { u.Id, u.Email, u.DisplayName, u.UserType, u.Status, u.LastSignInAt, u.CreatedAt })
            .ToListAsync(cancellationToken);

        // One more query for the page's role keys, rather than one per row.
        var ids = rows.Select(u => u.Id).ToList();
        var roleKeys = (await dbContext.UserRoles.AsNoTracking()
                .Where(ur => ids.Contains(ur.UserId))
                .Select(ur => new { ur.UserId, ur.Role.Key })
                .ToListAsync(cancellationToken))
            .ToLookup(ur => ur.UserId, ur => ur.Key);

        return PagedResult<UserSummaryDto>.Create(
            [.. rows.Select(u => new UserSummaryDto(
                u.Id, u.Email, u.DisplayName, u.UserType.ToString(), u.Status.ToString(), u.LastSignInAt, u.CreatedAt,
                [.. roleKeys[u.Id].Order(StringComparer.Ordinal)]))],
            page,
            totalCount);
    }

    /// <summary>
    /// A real DB-level idempotent insert (<c>INSERT ... ON CONFLICT
    /// (identity_platform_uid) DO NOTHING RETURNING ...</c>) rather than a
    /// read-then-write check. The unique index <c>ux_users_identity_platform_uid</c>
    /// makes Postgres the single point of truth for "does this UID already
    /// have a row", so there's no window for two concurrent requests to both
    /// decide "not found, insert".
    /// </summary>
    public async Task<ProvisionedUser> InsertIfAbsentAsync(User candidate, CancellationToken cancellationToken)
    {
        // Either this call wins the unique-index race and RETURNING hands back
        // the new row, or a concurrent winner already committed. Postgres's
        // ON CONFLICT check waits on the conflicting row's lock and only
        // resolves to DO NOTHING once that winner has committed, which leaves
        // this INSERT with zero rows.
        //
        // The RETURNING column list must be kept in sync with every property
        // EF maps on User. FromSqlInterpolated doesn't validate this at
        // compile time, so a mapped column missing here breaks at runtime,
        // not at build time.
        var inserted = await dbContext.Users
            .FromSqlInterpolated($"""
                INSERT INTO identity.users (identity_platform_uid, email, display_name, user_type, status, sign_in_provider, last_sign_in_at, created_at, updated_at)
                VALUES ({candidate.IdentityPlatformUid}, {candidate.Email}, {candidate.DisplayName}, {candidate.UserType.ToString()}, {candidate.Status.ToString()}, {candidate.SignInProvider}, {candidate.LastSignInAt}, {candidate.CreatedAt}, {candidate.UpdatedAt})
                ON CONFLICT (identity_platform_uid) DO NOTHING
                RETURNING id, identity_platform_uid, email, display_name, user_type, status, sign_in_provider, last_sign_in_at, created_at, updated_at
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (inserted is [var created])
        {
            return new ProvisionedUser(created, WasCreated: true);
        }

        // Conflict path: another request already provisioned this UID. This is
        // deliberately a *separate* statement rather than folded into the
        // INSERT's CTE (e.g. `WITH ins AS (INSERT ...) SELECT ... UNION ALL
        // SELECT ... FROM users WHERE NOT EXISTS (SELECT 1 FROM ins)`).
        //
        // Under Postgres Read Committed, a single statement takes one MVCC
        // snapshot at its start, and only the row the INSERT itself conflicted
        // on is re-checked against the latest committed data (EvalPlanQual). A
        // plain SELECT folded into the same command could still be evaluated
        // against the pre-wait snapshot and miss the winner's row entirely.
        //
        // As its own statement, this SELECT gets a fresh snapshot, taken after
        // our INSERT already waited on and observed the conflict. The winning
        // row necessarily committed before that wait resolved, so it is
        // guaranteed visible here. That holds whether or not the statement
        // runs inside an explicit multi-statement transaction.
        var existing = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(u => u.IdentityPlatformUid == candidate.IdentityPlatformUid, cancellationToken);

        return new ProvisionedUser(existing, WasCreated: false);
    }

    public Task<int?> GetIdByUidAsync(string identityPlatformUid, CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .Where(u => u.IdentityPlatformUid == identityPlatformUid)
            .Select(u => (int?)u.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, string>> GetEmailsAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken) =>
        userIds.Count == 0
            ? new Dictionary<int, string>()
            : await dbContext.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

    public Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public async Task<UserStatus?> UpdateStatusAsync(int userId, UserStatus status, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await GetByIdAsync(userId, cancellationToken) is not { } user)
        {
            return null;
        }

        var previous = user.Status;
        if (status == UserStatus.Disabled)
        {
            user.Disable(now);
        }
        else
        {
            user.Enable(now);
        }

        return previous;
    }
}
