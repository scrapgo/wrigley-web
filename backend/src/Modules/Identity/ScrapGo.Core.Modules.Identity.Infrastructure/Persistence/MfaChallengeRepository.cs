namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class MfaChallengeRepository(IdentityDbContext dbContext) : IMfaChallengeRepository
{
    public void Add(MfaChallenge challenge) => dbContext.MfaChallenges.Add(challenge);

    public Task<MfaChallenge?> FindAsync(int userId, DateTimeOffset authTime, CancellationToken cancellationToken) =>
        dbContext.MfaChallenges.SingleOrDefaultAsync(c => c.UserId == userId && c.AuthTime == authTime, cancellationToken);

    public Task<bool> IsVerifiedAsync(int userId, DateTimeOffset authTime, CancellationToken cancellationToken) =>
        dbContext.MfaChallenges.AsNoTracking()
            .AnyAsync(c => c.UserId == userId && c.AuthTime == authTime && c.VerifiedAt != null, cancellationToken);
}
