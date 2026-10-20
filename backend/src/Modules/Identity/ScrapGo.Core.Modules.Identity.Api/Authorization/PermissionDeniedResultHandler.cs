using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Application;
using ScrapGo.Core.Modules.Identity.Application.Abstractions;
using ScrapGo.Core.Modules.Identity.Application.Access;
using ScrapGo.Core.Shared.Kernel.Audit;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Turns every authorization 403 into ProblemDetails with
/// <c>reason: missing_permission</c> and writes an <c>access.denied</c> audit
/// row (ACCESS-MODEL.md section 9). Challenges (401) and successes go to the
/// framework's default handler unchanged.
/// </summary>
public sealed class PermissionDeniedResultHandler : IAuthorizationMiddlewareResultHandler
{
    public const string MissingPermissionReason = "missing_permission";

    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var required = policy.Requirements.OfType<PermissionRequirement>().SelectMany(r => r.AnyOf).Distinct().ToList();
        await AuditAsync(context, required);

        await ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
            StatusCodes.Status403Forbidden,
            "Missing permission",
            required.Count > 0
                ? $"Requires one of: {string.Join(", ", required)}."
                : "You don't have access to this resource.",
            MissingPermissionReason));
    }

    private static async Task AuditAsync(HttpContext context, IReadOnlyList<string> required)
    {
        var services = context.RequestServices;
        var access = await services.GetRequiredService<ICurrentAccess>().GetAsync(context.RequestAborted);

        services.GetRequiredService<IAuditLog<IdentityModule>>().Record(new AuditEvent(
            IdentityAuditEventTypes.AccessDenied,
            UserId: access?.UserId,
            Metadata: JsonSerializer.Serialize(new
            {
                method = context.Request.Method,
                path = context.Request.Path.Value,
                required,
                reason = MissingPermissionReason,
            })));
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(context.RequestAborted);
    }
}
