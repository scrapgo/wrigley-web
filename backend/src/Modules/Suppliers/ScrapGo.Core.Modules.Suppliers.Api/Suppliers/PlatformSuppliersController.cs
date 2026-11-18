using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace ScrapGo.Core.Modules.Suppliers.Api.Suppliers;

/// <summary>
/// Suppliers (Quickbase table <c>bqrcgnatz</c>), read-only, for holders of
/// <c>suppliers.view_all</c> (every supplier) or <c>suppliers.view_own</c>
/// (only their linked suppliers). A supplier outside the caller's scope is 404
/// <c>supplier_not_found</c>, on the record and on every sub-resource.
/// </summary>
[ApiController]
[RequirePermission(SupplierPermissions.ViewAll, SupplierPermissions.ViewOwn)]
[Route("api/suppliers")]
[Produces("application/json")]
public sealed class PlatformSuppliersController(SupplierService suppliers) : ControllerBase
{
    /// <summary>
    /// Suppliers with a name, sorted by name, one page at a time, each a full
    /// supplier record (the same fields as <c>GET /api/suppliers/{recordId}</c>).
    /// <c>search</c> filters by a name substring; <c>top</c> is 1 to 1000 (default 100).
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
        var result = await suppliers.ListAsync(new ListSuppliersQuery(search, skip, top), cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, Page: { } page }
            ? Ok(page)
            : SupplierProblems.For(result.Outcome);
    }

    /// <summary>One supplier by its Quickbase Record ID#.</summary>
    [HttpGet("{recordId:int}")]
    [ProducesResponseType<SupplierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Get(int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetAsync(recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, Supplier: { } supplier }
            ? Ok(supplier)
            : SupplierProblems.For(result.Outcome);
    }

    /// <summary>One supplier's Target Pricing — Progress Rail: target material, offer, freight, prices per unit, PO number.</summary>
    [HttpGet("{recordId:int}/target-pricing-progress-rail")]
    [ProducesResponseType<SupplierTargetPricingProgressRailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetTargetPricingProgressRail(int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetTargetPricingProgressRailAsync(recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, TargetPricingProgressRail: { } pricing }
            ? Ok(pricing)
            : SupplierProblems.For(result.Outcome);
    }

    /// <summary>
    /// The supplier's in-stock items (Quickbase In-Stock Items table). An empty
    /// list when it has none.
    /// </summary>
    [HttpGet("{recordId:int}/in-stock-items")]
    [ProducesResponseType<SupplierInStockItemsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetInStockItems(int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetInStockItemsAsync(recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, InStockItems: { } items }
            ? Ok(items)
            : SupplierProblems.For(result.Outcome);
    }

    /// <summary>The supplier's contacts (Quickbase Contacts table), main contact first. An empty list when it has none.</summary>
    [HttpGet("{recordId:int}/contacts")]
    [ProducesResponseType<SupplierContactsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetContacts(int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetContactsAsync(recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, Contacts: { } contacts }
            ? Ok(contacts)
            : SupplierProblems.For(result.Outcome);
    }

    /// <summary>The supplier's call notes history (Quickbase Call Notes table), newest first. An empty list when it has none.</summary>
    [HttpGet("{recordId:int}/call-notes-history")]
    [ProducesResponseType<SupplierCallNotesHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetCallNotesHistory(int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetCallNotesHistoryAsync(recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, CallNotesHistory: { } history }
            ? Ok(history)
            : SupplierProblems.For(result.Outcome);
    }

    /// <summary>
    /// The ITS dispatch records linked to the supplier (Quickbase ITS Dispatch
    /// Records table), newest ship date first. An empty list when there are none.
    /// <c>status</c> (repeatable, exact text such as <c>Delivered</c>) keeps only those
    /// statuses; without it every status is returned.
    /// </summary>
    [HttpGet("{recordId:int}/linked-its-dispatch-records")]
    [EndpointSummary("The supplier's ITS dispatch loads, optionally filtered by status")]
    [EndpointDescription(
        "Every load linked to the supplier (ITS Dispatch field 366), newest ship date first, with every field on the load form. " +
        "An empty list when there are none.\n\n" +
        "Filter by status with the `status` query parameter, a status's exact text. Repeat it for several (OR-ed):\n\n" +
        "- `GET /api/suppliers/17511/linked-its-dispatch-records`: every status\n" +
        "- `GET /api/suppliers/17511/linked-its-dispatch-records?status=Delivered`: Delivered loads only\n" +
        "- `GET /api/suppliers/17511/linked-its-dispatch-records?status=On%20Route&status=Loading`: On Route or Loading\n\n" +
        "Valid values come from `GET /api/suppliers/linked-its-dispatch-records/status-filters`. " +
        "An unknown value is 400 `invalid_status`.")]
    [ProducesResponseType<SupplierDispatchRecordsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetLinkedDispatchRecords(
        int recordId,
        [FromQuery(Name = "status")]
        [Description("Optional, repeatable. A load status's exact text, e.g. Delivered or On Route. Omit it for every status.")]
        string[]? status,
        CancellationToken cancellationToken)
    {
        var result = await suppliers.GetDispatchRecordsAsync(recordId, status, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, DispatchRecords: { } records }
            ? Ok(records)
            : SupplierProblems.For(result.Outcome);
    }

    /// <summary>
    /// The supplier Dashboard's stats, from its delivered ITS Dispatch loads: total
    /// paid this year, pending payment, loads in the last 90 days and before, and
    /// the current offer. Zeros and nulls when the supplier has no loads. <c>status</c>
    /// (repeatable, exact text) picks the load statuses counted; Delivered without it.
    /// </summary>
    [HttpGet("{recordId:int}/dashboard-stats")]
    [EndpointSummary("The supplier Dashboard's stats, for loads of the given status (Delivered by default)")]
    [EndpointDescription(
        "Total paid this year, pending payment, load counts and dollar totals for the last 90 days and before, and the current offer, " +
        "worked out from the supplier's ITS Dispatch loads. Zeros and nulls when the supplier has no loads.\n\n" +
        "Pick the load statuses counted with the `status` query parameter, a status's exact text. Repeat it for several (OR-ed):\n\n" +
        "- `GET /api/suppliers/17511/dashboard-stats`: Delivered loads (the default)\n" +
        "- `GET /api/suppliers/17511/dashboard-stats?status=Completed`: Completed loads\n" +
        "- `GET /api/suppliers/17511/dashboard-stats?status=Delivered&status=Invoiced`: Delivered or Invoiced\n\n" +
        "Valid values come from `GET /api/suppliers/linked-its-dispatch-records/status-filters`. " +
        "An unknown value is 400 `invalid_status`.")]
    [ProducesResponseType<SupplierDashboardStatsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetDashboardStats(
        int recordId,
        [FromQuery(Name = "status")]
        [Description("Optional, repeatable. A load status's exact text, e.g. Delivered or Completed. Omit it to count Delivered loads.")]
        string[]? status,
        CancellationToken cancellationToken)
    {
        var result = await suppliers.GetDashboardStatsAsync(recordId, status, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, Stats: { } stats }
            ? Ok(stats)
            : SupplierProblems.For(result.Outcome);
    }

    /// <summary>One supplier's yard capabilities: crusher, baler, scale, loading options and so on (all yes/no).</summary>
    [HttpGet("{recordId:int}/yard-capabilities")]
    [ProducesResponseType<SupplierYardCapabilitiesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetYardCapabilities(int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetYardCapabilitiesAsync(recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, YardCapabilities: { } capabilities }
            ? Ok(capabilities)
            : SupplierProblems.For(result.Outcome);
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
    public async Task<IActionResult> GetOtherProspectInformation(int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetOtherProspectInformationAsync(recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, OtherProspectInformation: { } information }
            ? Ok(information)
            : SupplierProblems.For(result.Outcome);
    }

    /// <summary>One supplier's call and prospect status: last call result, objection, call back date, notes.</summary>
    [HttpGet("{recordId:int}/call-prospect-status")]
    [ProducesResponseType<SupplierCallProspectStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetCallProspectStatus(int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetCallProspectStatusAsync(recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, CallProspectStatus: { } status }
            ? Ok(status)
            : SupplierProblems.For(result.Outcome);
    }
}
