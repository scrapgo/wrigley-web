using Microsoft.Extensions.Logging;

namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>
/// Suppliers from the Quickbase Suppliers table, through the shared
/// <see cref="IQuickbaseQueryService"/> (cached, resilient). Never calls
/// Quickbase directly.
/// </summary>
public sealed class QuickbaseSupplierSource(IQuickbaseQueryService quickbase, ILogger<QuickbaseSupplierSource> logger) : ISupplierSource
{
    public async Task<Sourced<SupplierDto?>> FindByRecordIdAsync(int recordId, CancellationToken cancellationToken)
    {
        var result = await QueryAsync(
            new QuickbaseQuery(SuppliersTable.TableId, SuppliersTable.DetailFields, SuppliersTable.ByRecordId(recordId)),
            cancellationToken);

        using var document = JsonDocument.Parse(result.ResponseJson);
        var supplier = Rows(document).Select(row => ToSupplier(new QuickbaseRecord(row))).FirstOrDefault();

        return new(supplier, Freshness(result));
    }

    public async Task<Sourced<SupplierCallProspectStatusDto?>> FindCallProspectStatusAsync(int recordId, CancellationToken cancellationToken)
    {
        var result = await QueryAsync(
            new QuickbaseQuery(SuppliersTable.TableId, SuppliersTable.CallProspectStatusFields, SuppliersTable.ByRecordId(recordId)),
            cancellationToken);

        using var document = JsonDocument.Parse(result.ResponseJson);
        var status = Rows(document).Select(row => ToCallProspectStatus(new QuickbaseRecord(row))).FirstOrDefault();

        return new(status, Freshness(result));
    }

    public async Task<Sourced<SupplierYardCapabilitiesDto?>> FindYardCapabilitiesAsync(int recordId, CancellationToken cancellationToken)
    {
        var result = await QueryAsync(
            new QuickbaseQuery(SuppliersTable.TableId, SuppliersTable.YardCapabilitiesFields, SuppliersTable.ByRecordId(recordId)),
            cancellationToken);

        using var document = JsonDocument.Parse(result.ResponseJson);
        var capabilities = Rows(document).Select(row => ToYardCapabilities(new QuickbaseRecord(row))).FirstOrDefault();

        return new(capabilities, Freshness(result));
    }

    public async Task<Sourced<SupplierTargetPricingProgressRailDto?>> FindTargetPricingProgressRailAsync(
        int recordId, CancellationToken cancellationToken)
    {
        var result = await QueryAsync(
            new QuickbaseQuery(SuppliersTable.TableId, SuppliersTable.TargetPricingProgressRailFields, SuppliersTable.ByRecordId(recordId)),
            cancellationToken);

        using var document = JsonDocument.Parse(result.ResponseJson);
        var pricing = Rows(document).Select(row => ToTargetPricingProgressRail(new QuickbaseRecord(row))).FirstOrDefault();

        return new(pricing, Freshness(result));
    }

    public async Task<Sourced<SupplierPage>> ListAsync(string? search, int skip, int top, CancellationToken cancellationToken)
    {
        var result = await QueryAsync(
            new QuickbaseQuery(
                SuppliersTable.TableId,
                SuppliersTable.ListFields,
                SuppliersTable.NamedLike(search),
                [new QuickbaseSort(SuppliersTable.Account)],
                Skip: skip,
                Top: top),
            cancellationToken);

        using var document = JsonDocument.Parse(result.ResponseJson);
        var items = Rows(document)
            .Select(row => new QuickbaseRecord(row))
            .Where(record => record.Int(SuppliersTable.RecordId) is not null)
            .Select(record => new SupplierSummaryDto(
                record.Int(SuppliersTable.RecordId)!.Value,
                record.Text(SuppliersTable.Account) ?? string.Empty))
            .ToList();

        var total = document.RootElement.TryGetProperty("metadata", out var metadata)
            && metadata.TryGetProperty("totalRecords", out var totalRecords)
            && totalRecords.TryGetInt32(out var count)
                ? count
                : skip + items.Count;

        return new(new SupplierPage(items, total), Freshness(result));
    }

    private async Task<QuickbaseQueryResult> QueryAsync(QuickbaseQuery query, CancellationToken cancellationToken)
    {
        try
        {
            return await quickbase.QueryAsync(query, cancellationToken: cancellationToken);
        }
        catch (QuickbaseApiException ex)
        {
            throw new SupplierSourceUnavailableException($"Quickbase query on {query.TableId} failed (status {ex.StatusCode}).", ex);
        }
    }

    private static IEnumerable<JsonElement> Rows(JsonDocument document) =>
        document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray()
            : [];

    private SupplierDto? ToSupplier(QuickbaseRecord record) =>
        record.Int(SuppliersTable.RecordId) is { } recordId
            ? new SupplierDto(
                recordId,
                record.Text(SuppliersTable.Account),
                record.Text(SuppliersTable.StreetAddress),
                record.Text(SuppliersTable.City),
                record.Text(SuppliersTable.State),
                record.Text(SuppliersTable.Country),
                record.Text(SuppliersTable.ZipCode),
                record.Text(SuppliersTable.MainContactPhone),
                record.TextList(SuppliersTable.MainContactNames),
                PaymentTermsOf(recordId, record.Text(SuppliersTable.PaymentTerms)),
                record.Text(SuppliersTable.MainEmail),
                record.User(SuppliersTable.LeadAssignedTo),
                record.Int(SuppliersTable.RelevantConsumerDistances),
                record.Text(SuppliersTable.InStockItemRecords),
                record.Int(SuppliersTable.TotalActivities),
                record.Decimal(SuppliersTable.TargetConsumerPrice),
                record.Int(SuppliersTable.DeliveredLast90Days),
                record.Int(SuppliersTable.DeliveredBefore90Days),
                QuickbaseDeadFreight.FromQuickbase(record.Bool(SuppliersTable.DeadFreight)))
            : null;

    private static SupplierTargetPricingProgressRailDto? ToTargetPricingProgressRail(QuickbaseRecord record) =>
        record.Int(SuppliersTable.RecordId) is { } recordId
            ? new SupplierTargetPricingProgressRailDto(
                recordId,
                record.Text(SuppliersTable.TargetMaterial),
                record.Decimal(SuppliersTable.TargetBreakEven),
                record.Decimal(SuppliersTable.TargetOffer),
                record.Text(SuppliersTable.TargetUom),
                record.Decimal(SuppliersTable.TrucksPerWeek),
                record.Decimal(SuppliersTable.TargetFreightPerUom),
                record.Decimal(SuppliersTable.TargetFreightCost),
                record.Decimal(SuppliersTable.TargetConsumerPrice),
                record.Decimal(SuppliersTable.PriceInNetTons),
                record.Decimal(SuppliersTable.PriceInLbs),
                record.Decimal(SuppliersTable.PriceInCwt),
                record.Decimal(SuppliersTable.PriceInGrossTons),
                record.Text(SuppliersTable.TargetPoNumber),
                record.Decimal(SuppliersTable.PriceChangeFromPrior))
            : null;

    private static SupplierYardCapabilitiesDto? ToYardCapabilities(QuickbaseRecord record) =>
        record.Int(SuppliersTable.RecordId) is { } recordId
            ? new SupplierYardCapabilitiesDto(
                recordId,
                record.Bool(SuppliersTable.CrusherOnSite),
                record.Bool(SuppliersTable.LoggerOnSite),
                record.Bool(SuppliersTable.LoadFlatbeds),
                record.Bool(SuppliersTable.LoadDumps),
                record.Bool(SuppliersTable.MobileCrusher),
                record.Bool(SuppliersTable.CanExport),
                record.Bool(SuppliersTable.HasGaylordBoxes),
                record.Bool(SuppliersTable.BalerOnSite),
                record.Bool(SuppliersTable.HasScale),
                record.Bool(SuppliersTable.LoadVanTrailers),
                record.Bool(SuppliersTable.HasLoadWrap),
                record.Bool(SuppliersTable.UsesOwnTrucks),
                record.Bool(SuppliersTable.RailAccess))
            : null;

    private SupplierCallProspectStatusDto? ToCallProspectStatus(QuickbaseRecord record) =>
        record.Int(SuppliersTable.RecordId) is { } recordId
            ? new SupplierCallProspectStatusDto(
                recordId,
                record.Text(SuppliersTable.ContactWithDecisionMakerMade),
                record.Text(SuppliersTable.ProspectStatus),
                ChoiceOf(recordId, "last call result", LastCallResults.Catalog, record.Choice(SuppliersTable.LastCallResult)),
                ChoiceOf(recordId, "supplier objection", SupplierObjections.Catalog, record.Choice(SuppliersTable.SupplierObjections)),
                record.DateTime(SuppliersTable.CallBackDate),
                record.Text(SuppliersTable.ObjectionExplained),
                record.Text(SuppliersTable.CallNotes))
            : null;

    /// <summary>A dropdown field as its enum; unknown text is returned as null and logged.</summary>
    private TEnum? ChoiceOf<TEnum>(int recordId, string fieldName, DropdownCatalog<TEnum> catalog, string? text)
        where TEnum : struct, Enum
    {
        if (QuickbaseDropdowns.TryFromQuickbase(catalog, text, out var value))
        {
            return value;
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            logger.LogWarning("Supplier {RecordId} has {Field} {Value} that isn't a known value; returned as empty.", recordId, fieldName, text);
        }

        return null;
    }

    /// <summary>
    /// Field 320 as <see cref="PaymentTerms"/>. Text that isn't a known value is
    /// returned as null and logged, so the dropdown shows "not set" rather than
    /// a value Quickbase doesn't hold.
    /// </summary>
    private PaymentTerms? PaymentTermsOf(int recordId, string? text)
    {
        if (QuickbasePaymentTerms.TryFromQuickbase(text, out var terms))
        {
            return terms;
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            logger.LogWarning("Supplier {RecordId} has payment terms {PaymentTerms} that aren't a known value; returned as empty.", recordId, text);
        }

        return null;
    }

    private static DataFreshness Freshness(QuickbaseQueryResult result) => new(result.Source.ToString(), result.FetchedAt);
}
