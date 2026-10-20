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
    /// A token for someone who may read suppliers: a platform administrator
    /// signed in with Google Workspace, until supplier scoping (B13) arrives.
    /// </summary>
    public async Task<string> SeedSupplierReaderAsync()
    {
        var (uid, _) = await this.SeedPlatformAdministratorAsync();
        return CreateToken(uid);
    }

    public static string SuppliersPath(string suffix = "") => $"/api/suppliers{suffix}";
}
