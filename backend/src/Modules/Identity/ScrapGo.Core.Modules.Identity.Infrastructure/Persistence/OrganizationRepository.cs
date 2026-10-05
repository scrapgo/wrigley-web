namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class OrganizationRepository(IdentityDbContext dbContext) : IOrganizationRepository
{
    public void Add(Organization organization) => dbContext.Organizations.Add(organization);

    public void AddMembership(OrganizationMembership membership) => dbContext.OrganizationMemberships.Add(membership);

    public Task<Organization?> GetByIdAsync(int organizationId, CancellationToken cancellationToken) =>
        dbContext.Organizations.SingleOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

    public Task<OrganizationMembership?> FindMembershipAsync(int userId, int organizationId, CancellationToken cancellationToken) =>
        dbContext.OrganizationMemberships.SingleOrDefaultAsync(
            m => m.UserId == userId && m.OrganizationId == organizationId, cancellationToken);

    public void RemoveMembership(OrganizationMembership membership) => dbContext.OrganizationMemberships.Remove(membership);
}
