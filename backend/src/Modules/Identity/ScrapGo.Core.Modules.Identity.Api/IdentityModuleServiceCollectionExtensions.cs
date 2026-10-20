using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Access;
using ScrapGo.Core.Modules.Identity.Application.Abstractions;
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
        // perm: policies on demand, checked against the caller's access context.
        // The handler is scoped: it uses the request-scoped ICurrentAccess.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, PermissionDeniedResultHandler>();

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpUserContext>();

        // The caller's access context, loaded at most once per request.
        services.AddScoped<ICurrentAccess, HttpCurrentAccess>();

        return services;
    }
}
