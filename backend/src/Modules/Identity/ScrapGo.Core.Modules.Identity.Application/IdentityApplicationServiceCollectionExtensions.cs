using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Application.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Organizations;
using ScrapGo.Core.Modules.Identity.Application.Roles;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application;

public static class IdentityApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        // Scoped: every handler depends on the per-request DbContext through its repositories.
        services.AddScoped<ProvisionCurrentUserHandler>();
        services.AddScoped<EvaluateUserStatusGateHandler>();
        services.AddScoped<LinkProviderHandler>();

        services.AddScoped<CreateOrganizationHandler>();
        services.AddScoped<ListMyOrganizationsHandler>();
        services.AddScoped<EvaluateOrganizationMembershipHandler>();

        services.AddScoped<RoleService>();

        services.AddScoped<PermissionResolver>();
        services.AddScoped<ListPermissionsHandler>();

        return services;
    }
}
