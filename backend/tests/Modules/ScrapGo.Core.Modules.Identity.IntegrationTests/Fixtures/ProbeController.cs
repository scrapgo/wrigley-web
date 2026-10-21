using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Shared.Infrastructure.Authorization;

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

    /// <summary>The caller's supplier scope, as other modules see it through <see cref="IUserContext"/>.</summary>
    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet("supplier-scope")]
    public async Task<IActionResult> SupplierScope(
        [FromServices] ScrapGo.Core.Shared.Kernel.Security.IUserContext userContext, CancellationToken cancellationToken)
    {
        var scope = await userContext.GetSupplierScopeAsync(cancellationToken);
        return Ok(new { all = scope.IsAll, recordIds = scope.RecordIds.Order() });
    }

    /// <summary>Any of two test-only permissions (<c>perm:Spec.Alpha|Spec.Beta</c>).</summary>
    [RequirePermission(SpecPermissions.Alpha, SpecPermissions.Beta)]
    [HttpGet("any-of")]
    public IActionResult AnyOf() => Ok(new { ok = true });

    [RequirePermission(Permissions.UsersManage)]
    [HttpGet("users-manage")]
    public IActionResult UsersManage() => Ok(new { ok = true });

    /// <summary>No authorization attribute at all: only the fallback policy stands between it and an anonymous caller.</summary>
    [HttpGet("no-attribute")]
    public IActionResult NoAttribute() => Ok(new { ok = true });
}
