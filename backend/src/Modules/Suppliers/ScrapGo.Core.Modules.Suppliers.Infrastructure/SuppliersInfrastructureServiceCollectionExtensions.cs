using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ScrapGo.Core.Modules.Suppliers.Infrastructure;

public static class SuppliersInfrastructureServiceCollectionExtensions
{
    /// <summary>The supplier source: the Quickbase Suppliers table via the shared query service.</summary>
    public static IServiceCollection AddSuppliersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISupplierSource, QuickbaseSupplierSource>();

        services
            .AddOptions<ReportTriggerOptions>()
            .Bind(configuration.GetSection(ReportTriggerOptions.SectionName))
            .ValidateDataAnnotations();

        services
            .AddOptions<SupplierDashboardOptions>()
            .Bind(configuration.GetSection(SupplierDashboardOptions.SectionName));

        return services;
    }
}