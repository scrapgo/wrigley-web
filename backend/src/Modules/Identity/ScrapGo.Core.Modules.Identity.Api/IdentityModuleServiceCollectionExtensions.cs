using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Shared.Kernel.Security;

namespace ScrapGo.Core.Modules.Identity.Api;

public static class IdentityModuleServiceCollectionExtensions
{
    /// <summary>
    /// The Identity module's web-layer services: the <c>[RequirePermission]</c>
    /// engine and <see cref="IUserContext"/>, the caller context every other
    /// module checks before touching protected data.
    /// </summary>
    public static IServiceCollection AddIdentityApi(this IServiceCollection services)
    {
        // Scoped: it resolves permissions through the per-request DbContext.
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, OrganizationContextAuthorizationResultHandler>();

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpUserContext>();

        return services;
    }
}
