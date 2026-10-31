using ScrapGo.Core.Modules.Identity.Application.Invitations;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class InvitationRepository(IdentityDbContext dbContext) : IInvitationRepository
{
    public void Add(Invitation invitation) => dbContext.Invitations.Add(invitation);

    public Task<Invitation?> FindAsync(int invitationId, CancellationToken cancellationToken) =>
        dbContext.Invitations.SingleOrDefaultAsync(i => i.Id == invitationId, cancellationToken);

    public Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.Invitations.SingleOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public Task LockByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM identity.invitations WHERE token_hash = {tokenHash} FOR UPDATE", cancellationToken);

    public Task LockPendingByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = Invitation.NormalizeEmail(email);
        return dbContext.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM identity.invitations WHERE email = {normalized} AND status = 'Pending' FOR UPDATE", cancellationToken);
    }

    public Task<Invitation?> FindPendingByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = Invitation.NormalizeEmail(email);
        return dbContext.Invitations.SingleOrDefaultAsync(
            i => i.Email == normalized && i.Status == InvitationStatus.Pending, cancellationToken);
    }

    public Task ExpireLapsedAsync(string email, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.Invitations
            .Where(i => i.Email == email && i.Status == InvitationStatus.Pending && i.ExpiresAt <= now)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.Status, InvitationStatus.Expired), cancellationToken);

    public async Task<PagedResult<InvitationDto>> ListAsync(
        InvitationStatus? status, PageRequest page, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var invitations = dbContext.Invitations.AsNoTracking();

        // Filter on the effective status: a lapsed Pending invitation is Expired.
        invitations = status switch
        {
            InvitationStatus.Pending => invitations.Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAt > now),
            InvitationStatus.Expired => invitations.Where(i =>
                i.Status == InvitationStatus.Expired || (i.Status == InvitationStatus.Pending && i.ExpiresAt <= now)),
            { } other => invitations.Where(i => i.Status == other),
            null => invitations,
        };

        var totalCount = await invitations.CountAsync(cancellationToken);
        var rows = await invitations
            .OrderByDescending(i => i.Id)
            .Skip(page.Skip)
            .Take(page.ResolvedPageSize)
            .ToListAsync(cancellationToken);

        // Roles are few; read their keys once for the whole page.
        var roleKeys = await dbContext.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Key, cancellationToken);

        return PagedResult<InvitationDto>.Create(
            [
                .. rows.Select(invitation => InvitationDto.From(
                    invitation, invitation.RoleIds.Where(roleKeys.ContainsKey).Select(id => roleKeys[id]), now)),
            ],
            page,
            totalCount);
    }
}
