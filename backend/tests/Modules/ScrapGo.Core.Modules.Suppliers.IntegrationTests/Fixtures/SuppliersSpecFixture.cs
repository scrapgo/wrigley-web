using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScrapGo.Core.Modules.Identity.Domain.Authorization;
using ScrapGo.Core.Modules.Identity.Domain.Users;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Fixtures;

/// <summary>The full host with the real Identity permission engine, and a fake Quickbase.</summary>
public sealed class SuppliersSpecFixture : IdentitySpecFixture
{
    public FakeQuickbaseQueryService Quickbase { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services) =>
        services.Replace(ServiceDescriptor.Singleton<IQuickbaseQueryService>(Quickbase));

    /// <summary>A token for someone who sees every supplier: a Site Admin (<c>suppliers.view_all</c>).</summary>
    public async Task<string> SeedSupplierReaderAsync()
    {
        var (uid, _) = await this.SeedSiteAdminAsync();
        return CreateToken(uid);
    }

    /// <summary>A token for an internal ScrapGo User (<c>suppliers.view_all</c> through <c>scrapgo_user</c>).</summary>
    public async Task<string> SeedScrapGoUserAsync()
    {
        var (uid, userId) = await this.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
        await this.AssignRoleAsync(userId, await RoleIdAsync(DefaultRoleNames.ScrapGoUser));
        return CreateToken(uid);
    }

    /// <summary>
    /// A token for an external user with the <c>user</c> role (<c>suppliers.view_own</c>),
    /// linked to <paramref name="supplierRecordIds"/> (possibly none).
    /// </summary>
    public async Task<string> SeedExternalSupplierUserAsync(params int[] supplierRecordIds)
    {
        var (uid, userId) = await this.SeedUserAsync($"seller-{Guid.NewGuid():N}@acme.example");
        DbContext.UserRoles.Add(UserRole.Assign(userId, await RoleIdAsync(DefaultRoleNames.User), UserType.External, null, DateTimeOffset.UtcNow));
        DbContext.UserSuppliers.AddRange(supplierRecordIds.Select(id => UserSupplier.Link(userId, id, null, DateTimeOffset.UtcNow)));
        await DbContext.SaveChangesAsync();
        EvictAccess();
        return CreateToken(uid);
    }

    public static string SuppliersPath(string suffix = "") => $"/api/suppliers{suffix}";

    private Task<int> RoleIdAsync(string key) =>
        DbContext.Roles.AsNoTracking().Where(r => r.Key == key).Select(r => r.Id).SingleAsync();
}
