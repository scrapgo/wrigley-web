namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class InvitationRepository(IdentityDbContext dbContext) : IInvitationRepository
{
    public void Add(Invitation invitation) => dbContext.Invitations.Add(invitation);

    public Task<Invitation?> FindAsync(int organizationId, int invitationId, CancellationToken cancellationToken) =>
        dbContext.Invitations
            .Include(i => i.Grants)
            .SingleOrDefaultAsync(i => i.Id == invitationId && i.OrganizationId == organizationId, cancellationToken);

    public Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.Invitations
            .Include(i => i.Grants)
            .SingleOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public Task<Invitation?> FindPendingAsync(int organizationId, string emailNormalized, CancellationToken cancellationToken) =>
        dbContext.Invitations.SingleOrDefaultAsync(
            i => i.OrganizationId == organizationId && i.EmailNormalized == emailNormalized && i.Status == InvitationStatus.Pending,
            cancellationToken);

    public async Task<IReadOnlyList<Invitation>> ListPendingAsync(int organizationId, CancellationToken cancellationToken) =>
        await dbContext.Invitations
            .AsNoTracking()
            .Include(i => i.Grants)
            .Where(i => i.OrganizationId == organizationId && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.Id)
            .ToListAsync(cancellationToken);
}
