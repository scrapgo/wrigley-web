namespace ScrapGo.Core.Modules.Suppliers.Application.Abstractions;

/// <summary>
/// Where supplier records live: the Quickbase Suppliers table today
/// (Infrastructure's <c>QuickbaseSupplierSource</c>). Performs no
/// authorization; <see cref="SupplierService"/> checks the caller first.
/// </summary>
public interface ISupplierSource
{
    /// <summary>One supplier by its Record ID#, the only unique key; null value when there is none.</summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<SupplierDto?>> FindByRecordIdAsync(int recordId, CancellationToken cancellationToken);

    /// <summary>One supplier's call and prospect status by Record ID#; null value when there is no such supplier.</summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<SupplierCallProspectStatusDto?>> FindCallProspectStatusAsync(int recordId, CancellationToken cancellationToken);

    /// <summary>One supplier's yard capabilities by Record ID#; null value when there is no such supplier.</summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<SupplierYardCapabilitiesDto?>> FindYardCapabilitiesAsync(int recordId, CancellationToken cancellationToken);

    /// <summary>One supplier's Target Pricing — Progress Rail by Record ID#; null value when there is no such supplier.</summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<SupplierTargetPricingProgressRailDto?>> FindTargetPricingProgressRailAsync(int recordId, CancellationToken cancellationToken);

    /// <summary>
    /// One supplier's Other Prospect Information by Record ID#, with its most recent
    /// activity (Call Notes table); null value when there is no such supplier.
    /// </summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<SupplierOtherProspectInformationDto?>> FindOtherProspectInformationAsync(int recordId, CancellationToken cancellationToken);

    /// <summary>Every in-stock item of a supplier (In-Stock Items table, by Related Supplier); empty when it has none.</summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<IReadOnlyList<SupplierInStockItemDto>?>> ListInStockItemsAsync(int supplierRecordId, CancellationToken cancellationToken);

    /// <summary>Every contact of a supplier (Contacts table, by Related Supplier), main contact first; empty when it has none.</summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<IReadOnlyList<SupplierContactDto>?>> ListContactsAsync(int supplierRecordId, CancellationToken cancellationToken);

    /// <summary>Every call note of a supplier (Call Notes table, by Related Supplier), newest first; empty when it has none.</summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<IReadOnlyList<SupplierCallNoteDto>?>> ListCallNotesAsync(int supplierRecordId, CancellationToken cancellationToken);

    /// <summary>Every ITS dispatch record linked to a supplier (by field 366), newest ship date first; empty when there are none.</summary>
    /// <param name="statuses">Only records with one of these statuses (Quickbase text); empty for every status.</param>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<IReadOnlyList<SupplierDispatchRecordDto>?>> ListDispatchRecordsAsync(
        int supplierRecordId, IReadOnlyList<string> statuses, CancellationToken cancellationToken);

    /// <summary>
    /// Every ITS dispatch load of a supplier (by field 366) whose status is one of
    /// <paramref name="statuses"/>, newest Del Date first, all pages; empty when there are none.
    /// </summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<IReadOnlyList<DeliveredLoad>>> ListDeliveredLoadsAsync(
        int supplierRecordId, IReadOnlyList<string> statuses, CancellationToken cancellationToken);

    /// <summary>
    /// Suppliers with a name, sorted by name, optionally filtered by a name
    /// substring, one page at a time.
    /// </summary>
    /// <param name="recordIds">Only these Record IDs (the caller's scope); null for every supplier. Never empty.</param>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<SupplierPage>> ListAsync(
        string? search, int skip, int top, IReadOnlyCollection<int>? recordIds, CancellationToken cancellationToken);
}

/// <summary>A value plus how fresh it is (Quickbase data is served through a cache).</summary>
public sealed record Sourced<T>(T Value, DataFreshness Freshness);

/// <summary>One delivered ITS dispatch load, as the Dashboard stats need it. Null means empty in Quickbase.</summary>
/// <param name="RecordId">Field 3.</param>
/// <param name="LoadNumber">Field 6, "Load #".</param>
/// <param name="ShipperPo">Field 21, "Shipper PO Numbers".</param>
/// <param name="DeliveryDate">Field 15, "Del Date".</param>
/// <param name="PaymentAmount">Field 386, Buy Price × Purchase Weight in NT ("Payment Amount").</param>
/// <param name="PaymentDate">Field 572 (already honors Pay Date Override, 593).</param>
/// <param name="BuyPrice">Field 512, "Buy Price".</param>
/// <param name="Status">Field 8, e.g. "Delivered".</param>
/// <param name="PaymentAmountQuote">Field 514, "Quote Buy Price Total".</param>
public sealed record DeliveredLoad(
    int RecordId,
    string? LoadNumber,
    string? ShipperPo,
    DateOnly? DeliveryDate,
    decimal? PaymentAmount,
    DateOnly? PaymentDate,
    decimal? BuyPrice,
    string? Status,
    decimal? PaymentAmountQuote);

/// <param name="TotalRecords">Every matching record, not just this page.</param>
public sealed record SupplierPage(IReadOnlyList<SupplierDto> Items, int TotalRecords);

/// <summary>The supplier source failed (e.g. Quickbase down) and no cached copy could stand in.</summary>
public sealed class SupplierSourceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
