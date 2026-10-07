namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

/// <summary>Tracked catalog and entitlement entities, for mutation. Reads that project to DTOs live in <see cref="IAuthorizationQueries"/>.</summary>
public interface IApplicationRepository
{
    Task<CatalogApplication?> FindCatalogApplicationAsync(int applicationId, CancellationToken cancellationToken);

    /// <summary>A module of exactly this application, or null (including a module of another application).</summary>
    Task<CatalogModule?> FindCatalogModuleAsync(int applicationId, int moduleId, CancellationToken cancellationToken);

    /// <summary>The organization's row for the application, in any status, or null.</summary>
    Task<OrganizationApplication?> FindOrganizationApplicationAsync(int organizationId, int applicationId, CancellationToken cancellationToken);

    void AddOrganizationApplication(OrganizationApplication organizationApplication);

    Task<OrganizationApplicationModule?> FindOrganizationApplicationModuleAsync(
        int organizationApplicationId, int moduleId, CancellationToken cancellationToken);

    void AddOrganizationApplicationModule(OrganizationApplicationModule module);
}
