namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Marks a controller or action as platform administration: it acts on an
/// organization from outside it (by its <c>{organizationId}</c>), so the
/// organization membership guard must not apply. A platform administrator
/// isn't a member of the organizations they administer.
/// </summary>
/// <remarks>
/// It **never grants anything by itself**. Every action it covers must carry
/// <c>[RequirePermission(..., PlatformScope = true)]</c>, and a route-table spec
/// (<c>PlatformAdministrationRoutes</c>) fails the build's tests if one doesn't.
/// Keep these routes under <c>/api/admin/…</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class PlatformAdministrationAttribute : Attribute;
