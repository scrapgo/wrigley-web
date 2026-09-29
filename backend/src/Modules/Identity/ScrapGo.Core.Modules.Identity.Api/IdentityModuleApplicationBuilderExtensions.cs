using Microsoft.AspNetCore.Builder;
using ScrapGo.Core.Modules.Identity.Api.Middleware;

namespace ScrapGo.Core.Modules.Identity.Api;

public static class IdentityModuleApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Identity module's request gates. Call it after
    /// <c>UseRouting()</c> and <c>UseAuthentication()</c>, and before
    /// <c>UseAuthorization()</c>. The gates need the authenticated principal
    /// and the matched route, and must deny before any policy or endpoint runs.
    /// </summary>
    public static IApplicationBuilder UseIdentityModule(this IApplicationBuilder app) =>
        app
            // First: no point checking a disabled user's organization membership.
            .UseMiddleware<DisabledUserGateMiddleware>()
            .UseMiddleware<OrganizationMembershipGuardMiddleware>();
}
