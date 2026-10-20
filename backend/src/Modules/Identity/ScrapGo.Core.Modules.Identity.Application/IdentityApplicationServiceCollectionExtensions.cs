using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Application.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Invitations;
using ScrapGo.Core.Modules.Identity.Application.Session;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application;

public static class IdentityApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        // Scoped: every handler depends on the per-request DbContext through its repositories.
        services.AddScoped<StartSessionHandler>();
        services.AddScoped<GetMeHandler>();
        services.AddScoped<UserReadService>();
        services.AddScoped<InvitationService>();
        services.AddScoped<UserRoleAssignmentService>();
        services.AddScoped<UserStatusService>();
        services.AddScoped<EvaluateUserStatusGateHandler>();

        services.AddScoped<ListPermissionsHandler>();
        services.AddScoped<BootstrapSiteAdminHandler>();

        return services;
    }
}
