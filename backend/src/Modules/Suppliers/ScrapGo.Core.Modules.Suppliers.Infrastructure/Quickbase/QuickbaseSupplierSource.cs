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

    public async Task<Sourced<SupplierOtherProspectInformationDto?>> FindOtherProspectInformationAsync(
        int recordId, CancellationToken cancellationToken)
    {
        var supplierResult = await QueryAsync(
            new QuickbaseQuery(SuppliersTable.TableId, SuppliersTable.OtherProspectInformationFields, SuppliersTable.ByRecordId(recordId)),
            cancellationToken);

        using var supplierDocument = JsonDocument.Parse(supplierResult.ResponseJson);
        var found = Rows(supplierDocument)
            .Select(row => new QuickbaseRecord(row))
            .Where(record => record.Int(SuppliersTable.RecordId) is not null)
            .Cast<QuickbaseRecord?>()
            .FirstOrDefault();
        if (found is not { } supplier)
        {
            // No such supplier: skip the activities query.
            return new(null, Freshness(supplierResult));
        }

        // Quickbase can't join tables, so the most recent activity is a second query.
        var activityResult = await QueryAsync(
            new QuickbaseQuery(
                CallNotesTable.TableId,
                CallNotesTable.LatestActivityFields,
                CallNotesTable.BySupplier(recordId),
                [new QuickbaseSort(CallNotesTable.DateCreated, QuickbaseSortOrder.Descending)],
                Skip: 0,
                Top: 1),
            cancellationToken);

        using var activityDocument = JsonDocument.Parse(activityResult.ResponseJson);
        var latestActivity = Rows(activityDocument)
            .Select(row => new QuickbaseRecord(row))
            .Where(record => record.Int(CallNotesTable.RecordId) is not null)
            .Select(record => new SupplierLatestActivityDto(
                record.Int(CallNotesTable.RecordId)!.Value,
                record.Int(CallNotesTable.RelatedSupplier),
                record.Text(CallNotesTable.SupplierAssignedTo),
                record.DateTime(CallNotesTable.DateCreated)))
            .FirstOrDefault();

        var information = new SupplierOtherProspectInformationDto(
            supplier.Int(SuppliersTable.RecordId)!.Value,
            supplier.Text(SuppliersTable.LeadSource),
            supplier.Text(SuppliersTable.LastReassignedSupplier),
            supplier.Text(SuppliersTable.LastModifiedBy),
            supplier.Text(SuppliersTable.RelatedSupplierOffice),
            supplier.Bool(SuppliersTable.OutOfBusiness),
            latestActivity);

        return new(information, LeastFresh(supplierResult, activityResult));
    }

    public async Task<Sourced<IReadOnlyList<SupplierInStockItemDto>?>> ListInStockItemsAsync(
        int supplierRecordId, CancellationToken cancellationToken)
    {
        var result = await QueryAsync(
            new QuickbaseQuery(InStockItemsTable.TableId, InStockItemsTable.Fields, InStockItemsTable.BySupplier(supplierRecordId)),
            cancellationToken);

        using var document = JsonDocument.Parse(result.ResponseJson);
        IReadOnlyList<SupplierInStockItemDto> items =
            [.. Rows(document).Select(row => ToInStockItem(new QuickbaseRecord(row))).OfType<SupplierInStockItemDto>()];

        return new(items, Freshness(result));
    }

    public async Task<Sourced<IReadOnlyList<SupplierContactDto>?>> ListContactsAsync(
        int supplierRecordId, CancellationToken cancellationToken)
    {
        var result = await QueryAsync(
            new QuickbaseQuery(ContactsTable.TableId, ContactsTable.Fields, ContactsTable.BySupplier(supplierRecordId)),
            cancellationToken);

        using var document = JsonDocument.Parse(result.ResponseJson);

        // The main contact first, then by last and first name (case-insensitive), then record id.
        IReadOnlyList<SupplierContactDto> contacts =
        [
            .. Rows(document)
                .Select(row => new QuickbaseRecord(row))
                .Where(record => record.Int(ContactsTable.RecordId) is not null)
                .Select(record => new SupplierContactDto(
                    record.Int(ContactsTable.RecordId)!.Value,
                    record.Int(ContactsTable.RelatedSupplier),
                    record.Text(ContactsTable.LastName),
                    record.Text(ContactsTable.FirstName),
                    record.Text(ContactsTable.Title),
                    record.Text(ContactsTable.Phone),
                    record.Text(ContactsTable.Cell),
                    record.Text(ContactsTable.Email),
                    record.Text(ContactsTable.Notes),
                    record.Bool(ContactsTable.IsMain)))
                .OrderByDescending(contact => contact.IsMain == true)
                .ThenBy(contact => contact.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(contact => contact.FirstName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(contact => contact.RecordId),
        ];

        return new(contacts, Freshness(result));
    }

    public async Task<Sourced<IReadOnlyList<SupplierCallNoteDto>?>> ListCallNotesAsync(
        int supplierRecordId, CancellationToken cancellationToken)
    {
        var result = await QueryAsync(
            new QuickbaseQuery(CallNotesTable.TableId, CallNotesTable.Fields, CallNotesTable.BySupplier(supplierRecordId)),
            cancellationToken);

        using var document = JsonDocument.Parse(result.ResponseJson);

        // Newest first; notes without a date last, then by record id (newest records have the highest ids).
        IReadOnlyList<SupplierCallNoteDto> notes =
        [
            .. Rows(document)
                .Select(row => new QuickbaseRecord(row))
                .Where(record => record.Int(CallNotesTable.RecordId) is not null)
                .Select(record => new SupplierCallNoteDto(
                    record.Int(CallNotesTable.RecordId)!.Value,
                    record.DateTime(CallNotesTable.DateCreated),
                    record.Text(CallNotesTable.Notes),
                    record.Int(CallNotesTable.RelatedSupplier)))
                .OrderByDescending(note => note.DateCreated ?? DateTimeOffset.MinValue)
                .ThenByDescending(note => note.RecordId),
        ];

        return new(notes, Freshness(result));
    }

    public async Task<Sourced<IReadOnlyList<SupplierDispatchRecordDto>?>> ListDispatchRecordsAsync(
        int supplierRecordId, CancellationToken cancellationToken)
    {
        var result = await QueryAsync(
            new QuickbaseQuery(DispatchRecordsTable.TableId, DispatchRecordsTable.Fields, DispatchRecordsTable.BySupplier(supplierRecordId)),
            cancellationToken);

        using var document = JsonDocument.Parse(result.ResponseJson);

        // Newest ship date first; records without one last, then by record id (newest first).
        IReadOnlyList<SupplierDispatchRecordDto> records =
        [
            .. Rows(document)
                .Select(row => ToDispatchRecord(new QuickbaseRecord(row)))
                .OfType<SupplierDispatchRecordDto>()
                .OrderByDescending(record => record.ShipDate ?? DateOnly.MinValue)
                .ThenByDescending(record => record.RecordId),
        ];

        return new(records, Freshness(result));
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

    private SupplierInStockItemDto? ToInStockItem(QuickbaseRecord record)
    {
        if (record.Int(InStockItemsTable.RecordId) is not { } recordId)
        {
            return null;
        }

        var packaging = ChoiceOf(recordId, "packaging", InStockItemDropdowns.Preparations, record.Choice(InStockItemsTable.Packaging));
        return new SupplierInStockItemDto(
            recordId,
            record.Int(InStockItemsTable.RelatedSupplier),
            ChoiceOf(recordId, "material type", InStockItemDropdowns.MaterialTypes, record.Choice(InStockItemsTable.MaterialType)),
            packaging,
            packaging,
            record.Decimal(InStockItemsTable.Weight),
            ChoiceOf(recordId, "unit of measure", InStockItemDropdowns.UnitsOfMeasure, record.Choice(InStockItemsTable.UnitOfMeasure)),
            record.Decimal(InStockItemsTable.TargetOffer),
            ChoiceOf(recordId, "truck type", InStockItemDropdowns.TruckTypes, record.Choice(InStockItemsTable.TruckType)),
            record.Decimal(InStockItemsTable.TrucksPerWeek),
            record.Decimal(InStockItemsTable.Volume));
    }

    private SupplierDispatchRecordDto? ToDispatchRecord(QuickbaseRecord record)
    {
        if (record.Int(DispatchRecordsTable.RecordId) is not { } recordId)
        {
            return null;
        }

        var city = record.Text(DispatchRecordsTable.ConsumerCity);
        var state = record.Text(DispatchRecordsTable.ConsumerState);
        return new SupplierDispatchRecordDto(
            recordId,
            record.Text(DispatchRecordsTable.LoadNumber),
            DispatchRecordsTable.FormatConsumer(record.Text(DispatchRecordsTable.Consumer), city, state),
            city,
            state,
            record.Text(DispatchRecordsTable.CarrierDriver),
            record.Text(DispatchRecordsTable.Material),
            record.Decimal(DispatchRecordsTable.Weight),
            record.Date(DispatchRecordsTable.ShipDate),
            record.Date(DispatchRecordsTable.DeliveryDate),
            ChoiceOf(recordId, "dispatch status", DispatchStatuses.Catalog, record.Choice(DispatchRecordsTable.Status)));
    }

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

    /// <summary>
    /// Freshness of a response built from two queries: the earlier fetch, and the
    /// worse source (StaleCache, then Cache, then Quickbase).
    /// </summary>
    private static DataFreshness LeastFresh(QuickbaseQueryResult first, QuickbaseQueryResult second)
    {
        static int Staleness(QuickbaseResultSource source) => source switch
        {
            QuickbaseResultSource.StaleCache => 2,
            QuickbaseResultSource.Cache => 1,
            _ => 0,
        };

        var source = Staleness(first.Source) >= Staleness(second.Source) ? first.Source : second.Source;
        var fetchedAt = first.FetchedAt <= second.FetchedAt ? first.FetchedAt : second.FetchedAt;
        return new(source.ToString(), fetchedAt);
    }
}
