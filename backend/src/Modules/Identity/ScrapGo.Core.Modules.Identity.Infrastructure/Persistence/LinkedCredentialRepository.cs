namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class LinkedCredentialRepository(IdentityDbContext dbContext) : ILinkedCredentialRepository
{
    public Task<LinkedCredential?> FindAsync(int userId, string providerName, CancellationToken cancellationToken) =>
        dbContext.LinkedCredentials
            .SingleOrDefaultAsync(lc => lc.UserId == userId && lc.ProviderName == providerName, cancellationToken);

    public void Add(LinkedCredential linkedCredential) => dbContext.LinkedCredentials.Add(linkedCredential);
}
