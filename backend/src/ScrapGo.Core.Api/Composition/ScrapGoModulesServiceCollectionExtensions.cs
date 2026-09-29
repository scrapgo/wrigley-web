namespace ScrapGo.Core.Api.Composition;

/// <summary>
/// The single entry point that registers every module. This host is the only
/// project that references all of them, so it is the only place their layers
/// are composed. Each module still owns its own registrations.
/// </summary>
public static class ScrapGoModulesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared kernel and every module:
    /// <list type="number">
    /// <item><b>PostgreSQL:</b> one DbContext per module and schema (<c>audit</c>,
    /// <c>identity</c>, <c>quickbase</c>), all from <c>ConnectionStrings:Default</c>,
    /// which fails fast if it is missing.</item>
    /// <item><b>JWT authentication</b> (GCIP bearer tokens, RS256, 1-hour cap)
    /// and <b>permission-based authorization</b>: the <c>[RequirePermission]</c>
    /// handler, organization-context results, the disabled-user gate and the
    /// cross-tenant membership guard.</item>
    /// <item>The <b>Quickbase typed HTTP client</b> with a Polly resilience
    /// pipeline (retry with exponential backoff and jitter, timeouts, circuit
    /// breaker).</item>
    /// <item><b>Scoped application services:</b> <c>IQuickbaseQueryService</c>
    /// and the Identity handlers and <c>RoleService</c>.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Add a new module here with one line. Its own
    /// <c>Add{Module}Application</c>/<c>Add{Module}Infrastructure</c>
    /// extensions hold the details.
    /// </remarks>
    public static IServiceCollection AddScrapGoModules(this IServiceCollection services, IConfiguration config)
    {
        // Audit schema context, TimeProvider, and the ProblemDetails exception handler.
        services.AddSharedInfrastructure(config);

        services.AddIdentityModule(config);
        services.AddQuickbaseEngineModule(config);

        services.AddAuthorization();

        // Controllers live in the module Api assemblies, not in this host:
        // register one application part per module Api project.
        services
            .AddControllers()
            .AddApplicationPart(typeof(IdentityModuleServiceCollectionExtensions).Assembly)
            .AddApplicationPart(typeof(QuickbaseEngineModuleServiceCollectionExtensions).Assembly);

        return services;
    }

    /// <summary>
    /// Identity: the <c>identity</c> DbContext, GCIP JWT bearer, the permission
    /// authorization engine, and user, organization and role services.
    /// </summary>
    private static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration config) =>
        services
            .AddIdentityApplication()
            .AddIdentityInfrastructure(config)
            .AddIdentityApi();

    /// <summary>
    /// QuickbaseEngine: the <c>quickbase</c> DbContext (query cache), the
    /// resilient Quickbase typed client, and <c>IQuickbaseQueryService</c>.
    /// </summary>
    private static IServiceCollection AddQuickbaseEngineModule(this IServiceCollection services, IConfiguration config) =>
        services
            .AddQuickbaseEngineApplication()
            .AddQuickbaseEngineInfrastructure(config)
            .AddQuickbaseEngineApi();
}
