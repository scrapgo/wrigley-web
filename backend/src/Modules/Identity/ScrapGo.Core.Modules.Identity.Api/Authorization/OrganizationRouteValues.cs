namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// The route-value conventions scoping keys off. Any route with an
/// <c>{organizationId}</c> segment is automatically covered by the
/// cross-tenant membership guard, and is what an organization-scoped
/// <see cref="RequirePermissionAttribute"/> resolves against. A route that also
/// has <c>{applicationId}</c> is application-scoped: the guard requires the
/// organization to have that application, and permissions resolve within it.
/// A route that names them anything else (e.g. <c>{orgId}</c>, <c>{appId}</c>)
/// silently gets none of this.
/// </summary>
public static class OrganizationRouteValues
{
    public const string OrganizationId = "organizationId";

    public const string ApplicationId = "applicationId";

    public static bool TryGetOrganizationId(HttpContext context, out int organizationId)
    {
        organizationId = 0;

        return context.Request.RouteValues.TryGetValue(OrganizationId, out var raw)
            && raw is not null
            && int.TryParse(raw.ToString(), out organizationId);
    }

    public static bool HasOrganizationId(HttpContext context) =>
        context.Request.RouteValues.TryGetValue(OrganizationId, out var raw) && raw is not null;

    public static bool HasApplicationId(HttpContext context) =>
        context.Request.RouteValues.TryGetValue(ApplicationId, out var raw) && raw is not null;

    public static bool TryGetApplicationId(HttpContext context, out int applicationId)
    {
        applicationId = 0;

        return context.Request.RouteValues.TryGetValue(ApplicationId, out var raw)
            && raw is not null
            && int.TryParse(raw.ToString(), out applicationId);
    }
}
