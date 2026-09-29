namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
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
                INSERT INTO identity.users (identity_platform_uid, email, status, classification, created_at, updated_at)
                VALUES ({candidate.IdentityPlatformUid}, {candidate.Email}, {candidate.Status.ToString()}, {candidate.Classification.ToString()}, {candidate.CreatedAt}, {candidate.UpdatedAt})
                ON CONFLICT (identity_platform_uid) DO NOTHING
                RETURNING id, identity_platform_uid, email, status, classification, created_at, updated_at
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

    public Task<UserStatusSnapshot?> GetStatusByUidAsync(string identityPlatformUid, CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .Where(u => u.IdentityPlatformUid == identityPlatformUid)
            .Select(u => new UserStatusSnapshot(u.Id, u.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<int?> GetIdByUidAsync(string identityPlatformUid, CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .Where(u => u.IdentityPlatformUid == identityPlatformUid)
            .Select(u => (int?)u.Id)
            .SingleOrDefaultAsync(cancellationToken);
}
