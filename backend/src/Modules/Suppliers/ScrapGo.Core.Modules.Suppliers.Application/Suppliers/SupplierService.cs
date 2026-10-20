using Microsoft.Extensions.Logging;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

public enum SupplierOutcome
{
    Success,

    /// <summary>The caller lacks <see cref="SupplierPermissions.ViewAll"/>.</summary>
    Forbidden,

    NotFound,

    /// <summary>A paging value out of range.</summary>
    InvalidRequest,

    /// <summary>Quickbase failed and nothing cached could be served.</summary>
    SourceUnavailable,
}

public sealed record SupplierResult(SupplierOutcome Outcome, SupplierResponse? Supplier = null);

public sealed record SupplierListResult(SupplierOutcome Outcome, SupplierListResponse? Page = null);

public sealed record SupplierInStockItemsResult(SupplierOutcome Outcome, SupplierInStockItemsResponse? InStockItems = null);

public sealed record SupplierContactsResult(SupplierOutcome Outcome, SupplierContactsResponse? Contacts = null);

public sealed record SupplierCallNotesHistoryResult(
    SupplierOutcome Outcome, SupplierCallNotesHistoryResponse? CallNotesHistory = null);

public sealed record SupplierDispatchRecordsResult(
    SupplierOutcome Outcome, SupplierDispatchRecordsResponse? DispatchRecords = null);

public sealed record SupplierTargetPricingProgressRailResult(
    SupplierOutcome Outcome, SupplierTargetPricingProgressRailResponse? TargetPricingProgressRail = null);

public sealed record SupplierYardCapabilitiesResult(
    SupplierOutcome Outcome, SupplierYardCapabilitiesResponse? YardCapabilities = null);

public sealed record SupplierOtherProspectInformationResult(
    SupplierOutcome Outcome, SupplierOtherProspectInformationResponse? OtherProspectInformation = null);

public sealed record SupplierCallProspectStatusResult(
    SupplierOutcome Outcome, SupplierCallProspectStatusResponse? CallProspectStatus = null);

/// <param name="Search">Optional name substring (case-insensitive in Quickbase).</param>
public sealed record ListSuppliersQuery(string? Search, int? Skip, int? Top);

/// <summary>
/// Supplier reads for the portal. Every call checks the caller through
/// <see cref="IUserContext"/> before any Quickbase data is touched:
/// <see cref="SupplierPermissions.ViewAll"/>, until
/// supplier scoping (B13) arrives.
/// </summary>
public sealed class SupplierService(IUserContext userContext, ISupplierSource source, ILogger<SupplierService> logger)
{
    public const int DefaultTop = 100;

    public const int MaxTop = 1000;

    public async Task<SupplierResult> GetAsync(int recordId, CancellationToken cancellationToken)
    {
        var (outcome, found) = await ReadRecordAsync(recordId, source.FindByRecordIdAsync, cancellationToken);
        return found is { Value: { } supplier }
            ? new(outcome, new SupplierResponse(supplier, found.Freshness))
            : new(outcome);
    }

    /// <summary>The supplier's call and prospect status: last call result, objection, call back date, notes.</summary>
    public async Task<SupplierCallProspectStatusResult> GetCallProspectStatusAsync(
        int recordId, CancellationToken cancellationToken)
    {
        var (outcome, found) = await ReadRecordAsync(recordId, source.FindCallProspectStatusAsync, cancellationToken);
        return found is { Value: { } status }
            ? new(outcome, new SupplierCallProspectStatusResponse(status, found.Freshness))
            : new(outcome);
    }

    /// <summary>The supplier's yard capabilities: crusher, baler, scale, loading options and so on.</summary>
    public async Task<SupplierYardCapabilitiesResult> GetYardCapabilitiesAsync(
        int recordId, CancellationToken cancellationToken)
    {
        var (outcome, found) = await ReadRecordAsync(recordId, source.FindYardCapabilitiesAsync, cancellationToken);
        return found is { Value: { } capabilities }
            ? new(outcome, new SupplierYardCapabilitiesResponse(capabilities, found.Freshness))
            : new(outcome);
    }

    /// <summary>The supplier's Target Pricing — Progress Rail: target material, offer, freight, prices per unit, PO number.</summary>
    public async Task<SupplierTargetPricingProgressRailResult> GetTargetPricingProgressRailAsync(
        int recordId, CancellationToken cancellationToken)
    {
        var (outcome, found) = await ReadRecordAsync(recordId, source.FindTargetPricingProgressRailAsync, cancellationToken);
        return found is { Value: { } pricing }
            ? new(outcome, new SupplierTargetPricingProgressRailResponse(pricing, found.Freshness))
            : new(outcome);
    }

    /// <summary>
    /// The supplier's Other Prospect Information (Documents &amp; Other): lead source,
    /// reassignment, office, out of business, and its most recent activity.
    /// </summary>
    public async Task<SupplierOtherProspectInformationResult> GetOtherProspectInformationAsync(
        int recordId, CancellationToken cancellationToken)
    {
        var (outcome, found) = await ReadRecordAsync(recordId, source.FindOtherProspectInformationAsync, cancellationToken);
        return found is { Value: { } information }
            ? new(outcome, new SupplierOtherProspectInformationResponse(information, found.Freshness))
            : new(outcome);
    }

    /// <summary>
    /// The supplier's in-stock items. A supplier with none (or an unknown
    /// supplier id) returns an empty list, not 404: the items table can't tell
    /// the two apart.
    /// </summary>
    public async Task<SupplierInStockItemsResult> GetInStockItemsAsync(
        int recordId, CancellationToken cancellationToken)
    {
        var (outcome, found) = await ReadRecordAsync(recordId, source.ListInStockItemsAsync, cancellationToken);
        return found is { Value: { } items }
            ? new(outcome, new SupplierInStockItemsResponse(recordId, items, found.Freshness))
            : new(outcome);
    }

    /// <summary>
    /// The supplier's contacts, main contact first. A supplier with none (or an
    /// unknown supplier id) returns an empty list, not 404.
    /// </summary>
    public async Task<SupplierContactsResult> GetContactsAsync(int recordId, CancellationToken cancellationToken)
    {
        var (outcome, found) = await ReadRecordAsync(recordId, source.ListContactsAsync, cancellationToken);
        return found is { Value: { } contacts }
            ? new(outcome, new SupplierContactsResponse(recordId, contacts, found.Freshness))
            : new(outcome);
    }

    /// <summary>
    /// The supplier's call notes history, newest first. A supplier with none (or
    /// an unknown supplier id) returns an empty list, not 404.
    /// </summary>
    public async Task<SupplierCallNotesHistoryResult> GetCallNotesHistoryAsync(
        int recordId, CancellationToken cancellationToken)
    {
        var (outcome, found) = await ReadRecordAsync(recordId, source.ListCallNotesAsync, cancellationToken);
        return found is { Value: { } notes }
            ? new(outcome, new SupplierCallNotesHistoryResponse(recordId, notes, found.Freshness))
            : new(outcome);
    }

    /// <summary>
    /// The ITS dispatch records linked to the supplier (Documents &amp; Other),
    /// newest ship date first. None (or an unknown supplier id) returns an empty
    /// list, not 404.
    /// </summary>
    public async Task<SupplierDispatchRecordsResult> GetDispatchRecordsAsync(
        int recordId, CancellationToken cancellationToken)
    {
        var (outcome, found) = await ReadRecordAsync(recordId, source.ListDispatchRecordsAsync, cancellationToken);
        return found is { Value: { } records }
            ? new(outcome, new SupplierDispatchRecordsResponse(recordId, records, found.Freshness))
            : new(outcome);
    }

    /// <summary>Checks access, then reads one supplier record; NotFound for a missing record.</summary>
    private async Task<(SupplierOutcome Outcome, Sourced<T?>? Found)> ReadRecordAsync<T>(
        int recordId,
        Func<int, CancellationToken, Task<Sourced<T?>>> read,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return (SupplierOutcome.Forbidden, null);
        }

        if (recordId < 1)
        {
            return (SupplierOutcome.NotFound, null);
        }

        try
        {
            var found = await read(recordId, cancellationToken);
            return found.Value is null ? (SupplierOutcome.NotFound, null) : (SupplierOutcome.Success, found);
        }
        catch (SupplierSourceUnavailableException ex)
        {
            logger.LogWarning(ex, "Supplier {RecordId} could not be read from the supplier source.", recordId);
            return (SupplierOutcome.SourceUnavailable, null);
        }
    }

    public async Task<SupplierListResult> ListAsync(ListSuppliersQuery query, CancellationToken cancellationToken)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return new(SupplierOutcome.Forbidden);
        }

        var skip = query.Skip ?? 0;
        var top = query.Top ?? DefaultTop;
        if (skip < 0 || top < 1 || top > MaxTop)
        {
            return new(SupplierOutcome.InvalidRequest);
        }

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        try
        {
            var page = await source.ListAsync(search, skip, top, cancellationToken);
            return new(SupplierOutcome.Success, new SupplierListResponse(
                page.Value.Items, skip, top, page.Value.TotalRecords, page.Freshness));
        }
        catch (SupplierSourceUnavailableException ex)
        {
            logger.LogWarning(ex, "The supplier list could not be read from the supplier source.");
            return new(SupplierOutcome.SourceUnavailable);
        }
    }

    private Task<bool> CanReadAsync(CancellationToken cancellationToken) =>
        userContext.HasPermissionAsync(SupplierPermissions.ViewAll, cancellationToken);
}
