using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace ScrapGo.Core.Modules.Suppliers.Application;

public static class SuppliersApplicationServiceCollectionExtensions
{
    /// <summary>The Suppliers module's application services: <see cref="SupplierService"/> and the report trigger.</summary>
    public static IServiceCollection AddSuppliersApplication(this IServiceCollection services)
    {
        services
            .AddScoped<SupplierService>()
            .AddScoped<ReportTriggerService>();

        // The webhook client has no resilience pipeline: it is a one-shot
        // outbound call, so a plain HttpClient from the factory is enough.
        services.AddHttpClient(ReportTriggerService.HttpClientFactoryName);

        return services;
    }
}