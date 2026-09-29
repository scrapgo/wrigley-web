namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class OrganizationRepository(IdentityDbContext dbContext) : IOrganizationRepository
{
    public void Add(Organization organization) => dbContext.Organizations.Add(organization);

    public void AddMembership(OrganizationMembership membership) => dbContext.OrganizationMemberships.Add(membership);
}
