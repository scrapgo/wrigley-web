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
            .Join(dbContext.Roles.AsNoTracking(), i => i.RoleId, r => r.Id, (i, r) => new { Invitation = i, RoleKey = r.Key })
            .ToListAsync(cancellationToken);

        return PagedResult<InvitationDto>.Create(
            [.. rows.Select(row => InvitationDto.From(row.Invitation, row.RoleKey, now))], page, totalCount);
    }
}
