using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ScrapGo.Core.Modules.Suppliers.Api.Suppliers;

/// <summary>
/// The same supplier reads as <see cref="SuppliersController"/>, without an
/// organization or application in the route: for platform administrators
/// (<c>Admin.Access</c> at platform scope, which requires a Google Workspace
/// sign-in). The Quickbase Suppliers table is ScrapGo-wide.
/// </summary>
[ApiController]
[Authorize]
[Route("api/suppliers")]
[Produces("application/json")]
public sealed class PlatformSuppliersController(SupplierService suppliers) : ControllerBase
{
    /// <summary>
    /// Suppliers with a name, sorted by name, one page at a time. <c>search</c>
    /// filters by a name substring; <c>top</c> is 1 to 1000 (default 100).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<SupplierListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int? skip,
        [FromQuery] int? top,
        CancellationToken cancellationToken)
    {
        var result = await suppliers.ListAsync(new ListSuppliersQuery(SupplierScope.Platform, search, skip, top), cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, Page: { } page }
            ? Ok(page)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.PlatformRead);
    }

    /// <summary>One supplier by its Quickbase Record ID#.</summary>
    [HttpGet("{recordId:int}")]
    [ProducesResponseType<SupplierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Get(int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetAsync(SupplierScope.Platform, recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, Supplier: { } supplier }
            ? Ok(supplier)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.PlatformRead);
    }

    /// <summary>One supplier's Target Pricing — Progress Rail: target material, offer, freight, prices per unit, PO number.</summary>
    [HttpGet("{recordId:int}/target-pricing-progress-rail")]
    [ProducesResponseType<SupplierTargetPricingProgressRailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetTargetPricingProgressRail(int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetTargetPricingProgressRailAsync(SupplierScope.Platform, recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, TargetPricingProgressRail: { } pricing }
            ? Ok(pricing)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.PlatformRead);
    }

    /// <summary>One supplier's yard capabilities: crusher, baler, scale, loading options and so on (all yes/no).</summary>
    [HttpGet("{recordId:int}/yard-capabilities")]
    [ProducesResponseType<SupplierYardCapabilitiesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetYardCapabilities(int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetYardCapabilitiesAsync(SupplierScope.Platform, recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, YardCapabilities: { } capabilities }
            ? Ok(capabilities)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.PlatformRead);
    }

    /// <summary>One supplier's call and prospect status: last call result, objection, call back date, notes.</summary>
    [HttpGet("{recordId:int}/call-prospect-status")]
    [ProducesResponseType<SupplierCallProspectStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetCallProspectStatus(int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetCallProspectStatusAsync(SupplierScope.Platform, recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, CallProspectStatus: { } status }
            ? Ok(status)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.PlatformRead);
    }
}
