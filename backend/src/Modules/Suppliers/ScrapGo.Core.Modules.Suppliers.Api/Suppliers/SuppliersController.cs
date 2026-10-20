using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ScrapGo.Core.Modules.Suppliers.Api.Suppliers;

/// <summary>
/// Suppliers (Quickbase table <c>bqrcgnatz</c>) for an organization's
/// application, e.g. Downstream. Read-only for now.
/// </summary>
/// <remarks>
/// The route carries <c>{organizationId}</c> and <c>{applicationId}</c>, so the
/// membership guard runs first: 403 without an active membership, 404
/// <c>application_not_found</c> if the organization doesn't have the
/// application. <see cref="SupplierService"/> then requires
/// <c>Downstream.Suppliers.Read</c> there, which also needs the Suppliers
/// module enabled.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/organizations/{organizationId:int}/applications/{applicationId:int}/suppliers")]
[Produces("application/json")]
public sealed class SuppliersController(SupplierService suppliers) : ControllerBase
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
        int organizationId,
        int applicationId,
        [FromQuery] string? search,
        [FromQuery] int? skip,
        [FromQuery] int? top,
        CancellationToken cancellationToken)
    {
        var result = await suppliers.ListAsync(
            new ListSuppliersQuery(SupplierScope.ForApplication(organizationId, applicationId), search, skip, top), cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, Page: { } page }
            ? Ok(page)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.Read);
    }

    /// <summary>One supplier by its Quickbase Record ID#.</summary>
    [HttpGet("{recordId:int}")]
    [ProducesResponseType<SupplierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Get(int organizationId, int applicationId, int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetAsync(SupplierScope.ForApplication(organizationId, applicationId), recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, Supplier: { } supplier }
            ? Ok(supplier)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.Read);
    }

    /// <summary>One supplier's Target Pricing — Progress Rail: target material, offer, freight, prices per unit, PO number.</summary>
    [HttpGet("{recordId:int}/target-pricing-progress-rail")]
    [ProducesResponseType<SupplierTargetPricingProgressRailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetTargetPricingProgressRail(int organizationId, int applicationId, int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetTargetPricingProgressRailAsync(SupplierScope.ForApplication(organizationId, applicationId), recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, TargetPricingProgressRail: { } pricing }
            ? Ok(pricing)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.Read);
    }

    /// <summary>
    /// The supplier's in-stock items (Quickbase In-Stock Items table). An empty
    /// list when it has none.
    /// </summary>
    [HttpGet("{recordId:int}/in-stock-items")]
    [ProducesResponseType<SupplierInStockItemsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetInStockItems(int organizationId, int applicationId, int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetInStockItemsAsync(SupplierScope.ForApplication(organizationId, applicationId), recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, InStockItems: { } items }
            ? Ok(items)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.Read);
    }

    /// <summary>The supplier's contacts (Quickbase Contacts table), main contact first. An empty list when it has none.</summary>
    [HttpGet("{recordId:int}/contacts")]
    [ProducesResponseType<SupplierContactsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetContacts(int organizationId, int applicationId, int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetContactsAsync(SupplierScope.ForApplication(organizationId, applicationId), recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, Contacts: { } contacts }
            ? Ok(contacts)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.Read);
    }

    /// <summary>The supplier's call notes history (Quickbase Call Notes table), newest first. An empty list when it has none.</summary>
    [HttpGet("{recordId:int}/call-notes-history")]
    [ProducesResponseType<SupplierCallNotesHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetCallNotesHistory(int organizationId, int applicationId, int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetCallNotesHistoryAsync(SupplierScope.ForApplication(organizationId, applicationId), recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, CallNotesHistory: { } history }
            ? Ok(history)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.Read);
    }

    /// <summary>
    /// The ITS dispatch records linked to the supplier (Quickbase ITS Dispatch
    /// Records table), newest ship date first. An empty list when there are none.
    /// </summary>
    [HttpGet("{recordId:int}/linked-its-dispatch-records")]
    [ProducesResponseType<SupplierDispatchRecordsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetLinkedDispatchRecords(int organizationId, int applicationId, int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetDispatchRecordsAsync(SupplierScope.ForApplication(organizationId, applicationId), recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, DispatchRecords: { } records }
            ? Ok(records)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.Read);
    }

    /// <summary>One supplier's yard capabilities: crusher, baler, scale, loading options and so on (all yes/no).</summary>
    [HttpGet("{recordId:int}/yard-capabilities")]
    [ProducesResponseType<SupplierYardCapabilitiesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetYardCapabilities(int organizationId, int applicationId, int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetYardCapabilitiesAsync(SupplierScope.ForApplication(organizationId, applicationId), recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, YardCapabilities: { } capabilities }
            ? Ok(capabilities)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.Read);
    }

    /// <summary>
    /// One supplier's Other Prospect Information (Documents &amp; Other): lead source,
    /// reassignment, office, out of business, and its most recent activity.
    /// </summary>
    [HttpGet("{recordId:int}/other-prospect-information")]
    [ProducesResponseType<SupplierOtherProspectInformationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetOtherProspectInformation(int organizationId, int applicationId, int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetOtherProspectInformationAsync(SupplierScope.ForApplication(organizationId, applicationId), recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, OtherProspectInformation: { } information }
            ? Ok(information)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.Read);
    }

    /// <summary>One supplier's call and prospect status: last call result, objection, call back date, notes.</summary>
    [HttpGet("{recordId:int}/call-prospect-status")]
    [ProducesResponseType<SupplierCallProspectStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetCallProspectStatus(
        int organizationId, int applicationId, int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetCallProspectStatusAsync(
            SupplierScope.ForApplication(organizationId, applicationId), recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, CallProspectStatus: { } status }
            ? Ok(status)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.Read);
    }
}

/// <summary>ProblemDetails for supplier outcomes, shared by both supplier controllers.</summary>
internal static class SupplierProblems
{
    public static IActionResult For(SupplierOutcome outcome, string requiredPermission) => outcome switch
    {
        SupplierOutcome.Forbidden => ProblemResults.Create(
            StatusCodes.Status403Forbidden,
            "Missing permission",
            requiredPermission == SupplierPermissions.Read
                ? $"Requires {SupplierPermissions.Read} for this organization's application, with its Suppliers module enabled."
                : $"Requires {SupplierPermissions.PlatformRead} at platform scope (a platform administrator signed in with Google Workspace).",
            "missing_permission").ToActionResult(),
        SupplierOutcome.NotFound => ProblemResults.Create(
            StatusCodes.Status404NotFound,
            "Supplier not found",
            "No supplier with that record id.",
            "supplier_not_found").ToActionResult(),
        SupplierOutcome.InvalidRequest => ProblemResults.Create(
            StatusCodes.Status400BadRequest,
            "Invalid supplier list request",
            $"skip must be 0 or more and top 1 to {SupplierService.MaxTop}.",
            "invalid_request").ToActionResult(),
        SupplierOutcome.SourceUnavailable => ProblemResults.Create(
            StatusCodes.Status502BadGateway,
            "Quickbase unavailable",
            "Quickbase didn't answer and no cached copy was available. Try again shortly.",
            "quickbase_unavailable").ToActionResult(),
        _ => throw new InvalidOperationException($"Unhandled {nameof(SupplierOutcome)}: {outcome}."),
    };
}
