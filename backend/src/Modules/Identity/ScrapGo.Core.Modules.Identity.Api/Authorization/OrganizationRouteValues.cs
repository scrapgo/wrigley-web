namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// The route-value convention organization scoping keys off. Any route with
/// an <c>{organizationId}</c> segment is automatically covered by the
/// cross-tenant membership guard, and is what an organization-scoped
/// <see cref="RequirePermissionAttribute"/> resolves against. A route that
/// names it anything else (e.g. <c>{orgId}</c>) silently gets neither.
/// </summary>
public static class OrganizationRouteValues
{
    public const string OrganizationId = "organizationId";

    public static bool TryGetOrganizationId(HttpContext context, out int organizationId)
    {
        organizationId = 0;

        return context.Request.RouteValues.TryGetValue(OrganizationId, out var raw)
            && raw is not null
            && int.TryParse(raw.ToString(), out organizationId);
    }

    public static bool HasOrganizationId(HttpContext context) =>
        context.Request.RouteValues.TryGetValue(OrganizationId, out var raw) && raw is not null;
}
