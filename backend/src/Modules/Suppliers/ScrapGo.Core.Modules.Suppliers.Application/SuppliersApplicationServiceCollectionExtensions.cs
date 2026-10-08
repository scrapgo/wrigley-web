using Microsoft.Extensions.DependencyInjection;

namespace ScrapGo.Core.Modules.Suppliers.Application;

public static class SuppliersApplicationServiceCollectionExtensions
{
    /// <summary>The Suppliers module's application services: <see cref="SupplierService"/>.</summary>
    public static IServiceCollection AddSuppliersApplication(this IServiceCollection services) =>
        services.AddScoped<SupplierService>();
}
