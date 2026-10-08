namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>A supplier in a list: enough to pick one.</summary>
/// <param name="RecordId">Quickbase Record ID# (field 3), the only unique key. Names repeat.</param>
/// <param name="Account">The supplier's name (field 8), as stored.</param>
public sealed record SupplierSummaryDto(int RecordId, string Account);

/// <summary>A Quickbase user, e.g. who a lead is assigned to.</summary>
public sealed record SupplierUserDto(string? Id, string? Email, string? Name);

/// <summary>One supplier's details from the Quickbase Suppliers table. Null means empty in Quickbase.</summary>
/// <param name="PaymentTerms">Quickbase field 320, one of <see cref="Suppliers.PaymentTerms"/>; null when empty or not a known value.</param>
/// <param name="DeadFreight">Quickbase field 321 (checkbox): checked is <see cref="Suppliers.DeadFreight.NotExempt"/>, unchecked <see cref="Suppliers.DeadFreight.Exempt"/>; null only if Quickbase sent no value.</param>
public sealed record SupplierDto(
    int RecordId,
    string? Account,
    string? StreetAddress,
    string? City,
    string? State,
    string? Country,
    string? ZipCode,
    string? MainContactPhone,
    IReadOnlyList<string> MainContactNames,
    PaymentTerms? PaymentTerms,
    string? MainEmail,
    SupplierUserDto? LeadAssignedTo,
    int? RelevantConsumerDistances,
    string? InStockItemRecords,
    int? TotalActivities,
    decimal? TargetConsumerPrice,
    int? DeliveredLast90Days,
    int? DeliveredBefore90Days,
    DeadFreight? DeadFreight);

/// <param name="Source"><c>Cache</c>, <c>Quickbase</c> (fetched just now) or <c>StaleCache</c> (Quickbase failed; an older copy was served).</param>
/// <param name="FetchedAt">When the data was fetched from Quickbase.</param>
public sealed record DataFreshness(string Source, DateTimeOffset FetchedAt);

public sealed record SupplierResponse(SupplierDto Supplier, DataFreshness Freshness);

public sealed record SupplierListResponse(
    IReadOnlyList<SupplierSummaryDto> Items, int Skip, int Top, int TotalRecords, DataFreshness Freshness);
