using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Application.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application;

public static class IdentityApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        // Scoped: every handler depends on the per-request DbContext through its repositories.
        services.AddScoped<ProvisionCurrentUserHandler>();
        services.AddScoped<GetCurrentUserHandler>();
        services.AddScoped<UserReadService>();
        services.AddScoped<UserRoleAssignmentService>();
        services.AddScoped<UserStatusService>();
        services.AddScoped<EvaluateUserStatusGateHandler>();
        services.AddScoped<LinkProviderHandler>();

        services.AddScoped<PermissionResolver>();
        services.AddScoped<ListPermissionsHandler>();
        services.AddScoped<BootstrapPlatformAdministratorHandler>();

        return services;
    }
}
