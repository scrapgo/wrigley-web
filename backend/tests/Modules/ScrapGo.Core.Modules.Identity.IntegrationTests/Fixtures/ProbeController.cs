using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Api.Authorization;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Fixtures;

/// <summary>
/// Test-only routes that exercise the authorization machinery through the
/// real pipeline, without depending on some future business endpoint. They
/// replace the legacy <c>/api/_diag/*</c> routes that lived in the production
/// host. <see cref="IdentitySpecFixture"/> registers this assembly as an
/// application part, so these routes never exist in production.
/// </summary>
[ApiController]
[Route("api/_test")]
public sealed class ProbeController : ControllerBase
{
    /// <summary>
    /// Wires nothing but [Authorize]. The membership guard must still cover it,
    /// purely because the route carries {organizationId}.
    /// </summary>
    [Authorize]
    [AcceptVerbs("GET", "POST", "PUT", "DELETE", Route = "org-scoped/{organizationId:int}")]
    public IActionResult OrgScoped() => Ok(new { ok = true });

    [RequirePermission(Permissions.InvoiceRead)]
    [HttpGet("permission-scoped/{organizationId:int}")]
    public IActionResult PermissionScoped() => Ok(new { ok = true });

    /// <summary>An organization-scoped permission on a route with no {organizationId}: must be 400, not 403.</summary>
    [RequirePermission(Permissions.InvoiceRead)]
    [HttpGet("permission-scoped-no-org")]
    public IActionResult PermissionScopedWithoutOrganization() => Ok(new { ok = true });

    [RequirePermission(Permissions.AdminAccess, PlatformScope = true)]
    [HttpGet("platform-scoped")]
    public IActionResult PlatformScoped() => Ok(new { ok = true });

    /// <summary>No authorization attribute at all: only the fallback policy stands between it and an anonymous caller.</summary>
    [HttpGet("no-attribute")]
    public IActionResult NoAttribute() => Ok(new { ok = true });
}
