using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Fixtures;

/// <summary>The full host with the real Identity permission engine, and a fake Quickbase.</summary>
public sealed class SuppliersSpecFixture : IdentitySpecFixture
{
    public FakeQuickbaseQueryService Quickbase { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services) =>
        services.Replace(ServiceDescriptor.Singleton<IQuickbaseQueryService>(Quickbase));

    /// <summary>
    /// A member of a new organization that has Downstream with the given
    /// modules enabled, holding "Downstream Viewer" there.
    /// </summary>
    public async Task<(string Token, int OrganizationId)> SeedSupplierReaderAsync(params int[] moduleIds)
    {
        var (uid, userId, organizationId) = await this.SeedMemberAsync();
        await this.EntitleAsync(organizationId, DownstreamApplication.Id, moduleIds);
        await this.GrantApplicationRoleAsync(
            userId, await this.ApplicationRoleIdAsync(DownstreamApplication.ViewerRole), organizationId, DownstreamApplication.Id);
        return (CreateToken(uid), organizationId);
    }

    public static string SuppliersPath(int organizationId, string suffix = "") =>
        $"/api/organizations/{organizationId}/applications/{DownstreamApplication.Id}/suppliers{suffix}";
}
