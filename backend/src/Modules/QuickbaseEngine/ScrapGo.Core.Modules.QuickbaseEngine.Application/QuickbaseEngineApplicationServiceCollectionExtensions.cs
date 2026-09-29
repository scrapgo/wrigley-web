using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.QuickbaseEngine.Application.Queries;

namespace ScrapGo.Core.Modules.QuickbaseEngine.Application;

public static class QuickbaseEngineApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddQuickbaseEngineApplication(this IServiceCollection services)
    {
        // Scoped: it depends on the per-request DbContext through the cache store.
        services.AddScoped<IQuickbaseQueryService, QuickbaseQueryService>();

        return services;
    }
}
