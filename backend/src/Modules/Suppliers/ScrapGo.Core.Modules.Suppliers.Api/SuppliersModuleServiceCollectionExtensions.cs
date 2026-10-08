using Microsoft.Extensions.DependencyInjection;

namespace ScrapGo.Core.Modules.Suppliers.Api;

public static class SuppliersModuleServiceCollectionExtensions
{
    /// <summary>
    /// The Suppliers module's web-layer services. There are none yet; its
    /// controllers are discovered because the host registers this assembly as
    /// an MVC application part.
    /// </summary>
    public static IServiceCollection AddSuppliersApi(this IServiceCollection services) => services;
}
