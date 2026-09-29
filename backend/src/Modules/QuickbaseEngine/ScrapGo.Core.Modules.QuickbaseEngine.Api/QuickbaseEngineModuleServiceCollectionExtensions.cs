using Microsoft.Extensions.DependencyInjection;

namespace ScrapGo.Core.Modules.QuickbaseEngine.Api;

public static class QuickbaseEngineModuleServiceCollectionExtensions
{
    /// <summary>
    /// The QuickbaseEngine module's web-layer services. There are none yet. Its
    /// controllers are discovered because the host registers this assembly as
    /// an MVC application part.
    /// </summary>
    /// <remarks>
    /// Controllers added here must follow the module rules: call
    /// <see cref="IQuickbaseQueryService"/> only (never <c>IQuickbaseClient</c>
    /// or an <c>HttpClient</c>), and enforce the caller's table access first,
    /// because the query service itself does no authorization.
    /// </remarks>
    public static IServiceCollection AddQuickbaseEngineApi(this IServiceCollection services) => services;
}
