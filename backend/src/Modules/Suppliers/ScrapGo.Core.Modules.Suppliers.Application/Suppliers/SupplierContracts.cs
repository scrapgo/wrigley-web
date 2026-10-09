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

/// <summary>A supplier's call and prospect status (Quickbase Suppliers table). Null means empty in Quickbase.</summary>
/// <param name="ContactWithDecisionMakerMade">Field 197, "Contact with Decision Maker Has Been Made" (text).</param>
/// <param name="ProspectStatus">Field 192 (text).</param>
/// <param name="LastCallResult">Field 193 (dropdown); null when empty or not a known value.</param>
/// <param name="SupplierObjection">Field 236, "Supplier Objections" (dropdown); null when empty or not a known value.</param>
/// <param name="CallBackDate">Field 181 (date/time).</param>
/// <param name="ObjectionExplained">Field 238 (text).</param>
/// <param name="CallNotes">Field 97 (text).</param>
public sealed record SupplierCallProspectStatusDto(
    int RecordId,
    string? ContactWithDecisionMakerMade,
    string? ProspectStatus,
    LastCallResult? LastCallResult,
    SupplierObjection? SupplierObjection,
    DateTimeOffset? CallBackDate,
    string? ObjectionExplained,
    string? CallNotes);

/// <summary>
/// What a supplier's yard can do (Quickbase Suppliers table). Every field is a
/// Quickbase checkbox; null only if Quickbase sent no value.
/// </summary>
/// <param name="CrusherOnSite">Field 65, "Crusher on Site?".</param>
/// <param name="LoggerOnSite">Field 186, "Logger on Site?".</param>
/// <param name="LoadFlatbeds">Field 78, "Load Flatbeds?".</param>
/// <param name="LoadDumps">Field 182, "Load Dumps?".</param>
/// <param name="MobileCrusher">Field 225, "Mobile Crusher".</param>
/// <param name="CanExport">Field 204, "Can Export?".</param>
/// <param name="HasGaylordBoxes">Field 205, "Has Gaylord Boxes?".</param>
/// <param name="BalerOnSite">Field 185, "Baler on Site?".</param>
/// <param name="HasScale">Field 359, "Has Scale?".</param>
/// <param name="LoadVanTrailers">Field 183, "Load Van Trailers?".</param>
/// <param name="HasLoadWrap">Field 187, "Has Load Wrap?".</param>
/// <param name="UsesOwnTrucks">Field 184, "Use Own Trucks?".</param>
/// <param name="RailAccess">Field 230, "Rail Access".</param>
public sealed record SupplierYardCapabilitiesDto(
    int RecordId,
    bool? CrusherOnSite,
    bool? LoggerOnSite,
    bool? LoadFlatbeds,
    bool? LoadDumps,
    bool? MobileCrusher,
    bool? CanExport,
    bool? HasGaylordBoxes,
    bool? BalerOnSite,
    bool? HasScale,
    bool? LoadVanTrailers,
    bool? HasLoadWrap,
    bool? UsesOwnTrucks,
    bool? RailAccess);

public sealed record SupplierYardCapabilitiesResponse(SupplierYardCapabilitiesDto YardCapabilities, DataFreshness Freshness);

public sealed record SupplierCallProspectStatusResponse(SupplierCallProspectStatusDto CallProspectStatus, DataFreshness Freshness);

public sealed record SupplierListResponse(
    IReadOnlyList<SupplierSummaryDto> Items, int Skip, int Top, int TotalRecords, DataFreshness Freshness);
