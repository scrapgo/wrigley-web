using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Organizations;

namespace ScrapGo.Core.Modules.Identity.Api.Organizations;

public sealed record CreateOrganizationRequest(string? Name);

public sealed record CreatedOrganizationResponse(int Id);

/// <summary>
/// Organizations the caller creates or belongs to. Routes here carry no
/// <c>{organizationId}</c>, deliberately: they are how a caller gets its
/// first membership, so the membership guard must not apply.
/// </summary>
[ApiController]
[Authorize]
[Route("api/organizations")]
[Produces("application/json")]
public sealed class OrganizationsController : ControllerBase
{
    /// <summary>Creates an organization; the caller becomes its OrganizationAdministrator.</summary>
    [HttpPost]
    [ProducesResponseType<CreatedOrganizationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CreateOrganizationRequest request,
        [FromServices] CreateOrganizationHandler handler,
        CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var result = await handler.HandleAsync(
            new CreateOrganizationCommand(uid, User.FindFirst("email")?.Value ?? string.Empty, User.FindFirst("hd")?.Value, request.Name),
            cancellationToken);

        return result switch
        {
            { Outcome: CreateOrganizationOutcome.Created, OrganizationId: { } id } =>
                Created($"/api/organizations/{id}", new CreatedOrganizationResponse(id)),

            { Outcome: CreateOrganizationOutcome.InvalidName } => ProblemResults.Create(
                StatusCodes.Status400BadRequest,
                "Invalid organization name",
                "Name must be non-blank and contain at least one letter or digit.",
                "invalid_name").ToActionResult(),

            { Outcome: CreateOrganizationOutcome.DuplicateSlug } => ProblemResults.Create(
                StatusCodes.Status409Conflict,
                "Organization slug already exists",
                "An organization with this name (or an equivalent slug) already exists.",
                "duplicate_slug").ToActionResult(),

            _ => throw new InvalidOperationException($"Unhandled {nameof(CreateOrganizationOutcome)}: {result.Outcome}."),
        };
    }

    /// <summary>The caller's active organizations (active membership in an active organization).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizationSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMine([FromServices] ListMyOrganizationsHandler handler, CancellationToken cancellationToken) =>
        User.GetIdentityPlatformUid() is { } uid
            ? Ok(await handler.HandleAsync(uid, cancellationToken))
            : ControllerProblems.MissingSubjectClaim();
}
