using ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Fixtures;

/// <summary>
/// Test-only sample permissions. Production seeds no business permissions
/// (ORG-APP-MODULE-MODEL.md, Decision 7, blank slate) and the generic
/// <c>Invoice.*</c> / <c>Report.*</c> are retired. Specs that need "some
/// permission no endpoint gates on" use these instead.
/// </summary>
/// <remarks>
/// Inserted by <see cref="IdentitySpecFixture"/> after the migrations run, never
/// by a production migration. They are also granted to the built-in
/// OrganizationAdministrator **in the test database only**, so an org admin
/// can compose roles with them through the API, as they could with the
/// retired permissions before.
/// </remarks>
public static class SpecPermissions
{
    public const string Alpha = "Spec.Alpha";

    public const string Beta = "Spec.Beta";

    public const string Gamma = "Spec.Gamma";

    public static readonly string[] All = [Alpha, Beta, Gamma];

    /// <summary>Every row in the test database's catalog: production (retired included) plus these.</summary>
    public static IEnumerable<string> CatalogRows => Permissions.All.Concat(All).Concat(SpecApplications.Permissions);

    /// <summary>What <c>GET /api/permissions</c> lists: the live production catalog plus these.</summary>
    public static IEnumerable<string> Listed => Permissions.Active.Concat(All).Concat(SpecApplications.Permissions);

    /// <summary>The built-in OrganizationAdministrator's grants in the test database.</summary>
    public static IEnumerable<string> OrganizationAdministratorGrants =>
        Permissions.Active
            .Except(Permissions.PlatformOnly)
            .Except(Permissions.ApplicationScopeOnly)
            // Module permissions are held only through application roles.
            .Where(p => ApplicationCatalog.ModuleIdOf(p) is null)
            .Concat(All);

    /// <summary>Far above the positional production ids, so they never collide.</summary>
    private const int FirstId = 1001;

    internal static async Task SeedAsync(IdentityDbContext dbContext)
    {
        for (var i = 0; i < All.Length; i++)
        {
            await dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO identity.permissions (id, name, created_at, updated_at)
                VALUES ({FirstId + i}, {All[i]}, now(), now());
                """);
            await dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO identity.role_permissions (role_id, permission_id) VALUES (1, {FirstId + i});
                """);
        }
    }
}
