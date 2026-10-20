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
    /// <summary>Gated on a test-only permission no real endpoint uses.</summary>
    [RequirePermission(SpecPermissions.Alpha)]
    [HttpGet("permission-scoped")]
    public IActionResult PermissionScoped() => Ok(new { ok = true });

    [RequirePermission(Permissions.AdminAccess)]
    [HttpGet("platform-scoped")]
    public IActionResult PlatformScoped() => Ok(new { ok = true });

    /// <summary>No authorization attribute at all: only the fallback policy stands between it and an anonymous caller.</summary>
    [HttpGet("no-attribute")]
    public IActionResult NoAttribute() => Ok(new { ok = true });
}
