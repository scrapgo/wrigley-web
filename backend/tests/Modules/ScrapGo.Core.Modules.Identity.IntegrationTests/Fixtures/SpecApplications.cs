using ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Fixtures;

/// <summary>
/// A test-only application catalog. Production seeds no applications
/// (ORG-APP-MODULE-MODEL.md, Decision 7, blank slate), so the fixture inserts
/// these after the migrations, never a production migration:
/// <list type="bullet">
/// <item><b>SpecApp</b> (modules Alpha and Beta), with templates "SpecApp
/// Administrator" (<c>Application.ManageAccess</c> plus every SpecApp
/// permission) and "SpecApp Viewer" (<c>SpecApp.Alpha.Read</c> only).</item>
/// <item><b>OtherApp</b> (module Gamma), for cross-application checks, with an
/// "OtherApp Administrator" template.</item>
/// </list>
/// Nothing is assigned to any organization or granted to anyone: specs do that.
/// </summary>
public static class SpecApplications
{
    public const int SpecAppId = 900;
    public const int AlphaModuleId = 901;
    public const int BetaModuleId = 902;
    public const int OtherAppId = 910;
    public const int GammaModuleId = 911;

    public const string AlphaRead = "SpecApp.Alpha.Read";
    public const string AlphaWrite = "SpecApp.Alpha.Write";
    public const string BetaRead = "SpecApp.Beta.Read";
    public const string GammaRead = "OtherApp.Gamma.Read";

    public const string SpecAppAdministrator = "SpecApp Administrator";
    public const string SpecAppViewer = "SpecApp Viewer";
    public const string OtherAppAdministrator = "OtherApp Administrator";

    public static readonly string[] Permissions = [AlphaRead, AlphaWrite, BetaRead, GammaRead];

    internal static async Task SeedAsync(IdentityDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO identity.applications (id, key, name, status, created_at, updated_at) VALUES
                ({SpecAppId}, 'spec-app', 'SpecApp', 'Active', now(), now()),
                ({OtherAppId}, 'other-app', 'OtherApp', 'Active', now(), now());
            INSERT INTO identity.modules (id, application_id, key, name, status, created_at, updated_at) VALUES
                ({AlphaModuleId}, {SpecAppId}, 'alpha', 'Alpha', 'Active', now(), now()),
                ({BetaModuleId}, {SpecAppId}, 'beta', 'Beta', 'Active', now(), now()),
                ({GammaModuleId}, {OtherAppId}, 'gamma', 'Gamma', 'Active', now(), now());
            INSERT INTO identity.permissions (id, name, module_id, created_at, updated_at) VALUES
                (2001, {AlphaRead}, {AlphaModuleId}, now(), now()),
                (2002, {AlphaWrite}, {AlphaModuleId}, now(), now()),
                (2003, {BetaRead}, {BetaModuleId}, now(), now()),
                (2004, {GammaRead}, {GammaModuleId}, now(), now());
            """);

        await SeedTemplateAsync(dbContext, SpecAppId, SpecAppAdministrator,
            [Domain.Authorization.Permissions.ApplicationManageAccess, AlphaRead, AlphaWrite, BetaRead]);
        await SeedTemplateAsync(dbContext, SpecAppId, SpecAppViewer, [AlphaRead]);
        await SeedTemplateAsync(dbContext, OtherAppId, OtherAppAdministrator,
            [Domain.Authorization.Permissions.ApplicationManageAccess, GammaRead]);
    }

    private static async Task SeedTemplateAsync(IdentityDbContext dbContext, int applicationId, string name, string[] permissionNames)
    {
        var role = Role.CreateForApplication(organizationId: null, applicationId, name, description: null, DateTimeOffset.UtcNow);
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        var permissionIds = await dbContext.Permissions.Where(p => permissionNames.Contains(p.Name)).Select(p => p.Id).ToListAsync();
        dbContext.RolePermissions.AddRange(permissionIds.Select(id => RolePermission.Create(role.Id, id)));
        await dbContext.SaveChangesAsync();
    }
}
