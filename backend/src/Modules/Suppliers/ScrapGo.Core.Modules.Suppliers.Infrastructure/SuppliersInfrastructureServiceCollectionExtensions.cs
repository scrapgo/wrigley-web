using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

namespace ScrapGo.Core.Modules.Suppliers.Infrastructure;

public static class SuppliersInfrastructureServiceCollectionExtensions
{
    /// <summary>The supplier source: the Quickbase Suppliers table via the shared query service.</summary>
    public static IServiceCollection AddSuppliersInfrastructure(this IServiceCollection services) =>
        services.AddScoped<ISupplierSource, QuickbaseSupplierSource>();
}
