namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class ApplicationRepository(IdentityDbContext dbContext) : IApplicationRepository
{
    public Task<CatalogApplication?> FindCatalogApplicationAsync(int applicationId, CancellationToken cancellationToken) =>
        dbContext.CatalogApplications.SingleOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

    public Task<CatalogModule?> FindCatalogModuleAsync(int applicationId, int moduleId, CancellationToken cancellationToken) =>
        dbContext.CatalogModules.SingleOrDefaultAsync(m => m.Id == moduleId && m.ApplicationId == applicationId, cancellationToken);

    public Task<OrganizationApplication?> FindOrganizationApplicationAsync(
        int organizationId, int applicationId, CancellationToken cancellationToken) =>
        dbContext.OrganizationApplications.SingleOrDefaultAsync(
            oa => oa.OrganizationId == organizationId && oa.ApplicationId == applicationId, cancellationToken);

    public void AddOrganizationApplication(OrganizationApplication organizationApplication) =>
        dbContext.OrganizationApplications.Add(organizationApplication);

    public Task<OrganizationApplicationModule?> FindOrganizationApplicationModuleAsync(
        int organizationApplicationId, int moduleId, CancellationToken cancellationToken) =>
        dbContext.OrganizationApplicationModules.SingleOrDefaultAsync(
            m => m.OrganizationApplicationId == organizationApplicationId && m.ModuleId == moduleId, cancellationToken);

    public void AddOrganizationApplicationModule(OrganizationApplicationModule module) =>
        dbContext.OrganizationApplicationModules.Add(module);
}
