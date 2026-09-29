using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Shared.Infrastructure.Audit;
using ScrapGo.Core.Shared.Infrastructure.Persistence;
using ScrapGo.Core.Shared.Infrastructure.Web;

namespace ScrapGo.Core.Shared.Infrastructure;

public static class SharedInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = PostgresDbContextOptions.GetRequiredConnectionString(configuration);

        services.AddDbContext<AuditDbContext>(options =>
            options.UseModulePostgres(connectionString, AuditLogConfiguration.Schema));

        services.AddSingleton(TimeProvider.System);

        services.AddExceptionHandler<GenericExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}
