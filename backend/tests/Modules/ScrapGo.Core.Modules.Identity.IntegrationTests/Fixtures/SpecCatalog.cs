using ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Fixtures;

/// <summary>
/// Test-only sample permissions. Production seeds no business permissions and the generic
/// <c>Invoice.*</c> / <c>Report.*</c> are retired. Specs that need "some
/// permission no endpoint gates on" use these instead.
/// </summary>
/// <remarks>
/// Inserted by <see cref="IdentitySpecFixture"/> after the migrations run, never
/// by a production migration.
/// </remarks>
public static class SpecPermissions
{
    public const string Alpha = "Spec.Alpha";

    public const string Beta = "Spec.Beta";

    public const string Gamma = "Spec.Gamma";

    public static readonly string[] All = [Alpha, Beta, Gamma];

    /// <summary>Every row in the test database's catalog: production (retired included) plus these.</summary>
    public static IEnumerable<string> CatalogRows => Permissions.All.Concat(All);

    /// <summary>What <c>GET /api/permissions</c> lists: the live production catalog plus these.</summary>
    public static IEnumerable<string> Listed => Permissions.Active.Concat(All);

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
        }
    }
}
