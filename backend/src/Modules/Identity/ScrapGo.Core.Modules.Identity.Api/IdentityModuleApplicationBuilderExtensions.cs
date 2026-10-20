using Microsoft.AspNetCore.Builder;
using ScrapGo.Core.Modules.Identity.Api.Middleware;

namespace ScrapGo.Core.Modules.Identity.Api;

public static class IdentityModuleApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Identity module's request gate. Call it after
    /// <c>UseRouting()</c> and <c>UseAuthentication()</c>, and before
    /// <c>UseAuthorization()</c>. The gate needs the authenticated principal
    /// and must deny before any policy or endpoint runs.
    /// </summary>
    public static IApplicationBuilder UseIdentityModule(this IApplicationBuilder app) =>
        app.UseMiddleware<DisabledUserGateMiddleware>();
}
