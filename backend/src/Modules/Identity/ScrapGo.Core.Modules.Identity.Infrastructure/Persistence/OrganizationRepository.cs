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

    // Foreign keys are Restrict, so dependents go first, in key order.
    public Task DeleteWithDependentsAsync(int organizationId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync($"""
            DELETE FROM identity.organization_application_modules
            WHERE organization_application_id IN (SELECT id FROM identity.organization_applications WHERE organization_id = {organizationId});
            DELETE FROM identity.organization_applications WHERE organization_id = {organizationId};
            DELETE FROM identity.invitation_grants
            WHERE invitation_id IN (SELECT id FROM identity.invitations WHERE organization_id = {organizationId})
               OR role_id IN (SELECT id FROM identity.roles WHERE organization_id = {organizationId});
            DELETE FROM identity.invitations WHERE organization_id = {organizationId};
            DELETE FROM identity.user_roles
            WHERE organization_id = {organizationId}
               OR role_id IN (SELECT id FROM identity.roles WHERE organization_id = {organizationId});
            DELETE FROM identity.role_permissions
            WHERE role_id IN (SELECT id FROM identity.roles WHERE organization_id = {organizationId});
            DELETE FROM identity.roles WHERE organization_id = {organizationId};
            DELETE FROM identity.organization_memberships WHERE organization_id = {organizationId};
            DELETE FROM identity.organizations WHERE id = {organizationId};
            """, cancellationToken);
}
