using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Organizations;

namespace ScrapGo.Core.Modules.Identity.Api.Organizations;

/// <summary>
/// The organizations the caller belongs to. This route carries no
/// <c>{organizationId}</c>, deliberately: it spans the caller's memberships.
/// </summary>
/// <remarks>
/// Self-service creation (<c>POST /api/organizations</c>) was removed: only
/// platform administrators create organizations, through
/// <see cref="PlatformOrganizationsController"/> (ORG-APP-MODULE-MODEL.md,
/// Decision 11).
/// </remarks>
[ApiController]
[Authorize]
[Route("api/organizations")]
[Produces("application/json")]
public sealed class OrganizationsController : ControllerBase
{
    /// <summary>The caller's active organizations (active membership in an active organization).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizationSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMine([FromServices] ListMyOrganizationsHandler handler, CancellationToken cancellationToken) =>
        User.GetIdentityPlatformUid() is { } uid
            ? Ok(await handler.HandleAsync(uid, cancellationToken))
            : ControllerProblems.MissingSubjectClaim();
}
