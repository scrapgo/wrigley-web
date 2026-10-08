namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>
/// Suppliers from the Quickbase Suppliers table, through the shared
/// <see cref="IQuickbaseQueryService"/> (cached, resilient). Never calls
/// Quickbase directly.
/// </summary>
public sealed class QuickbaseSupplierSource(IQuickbaseQueryService quickbase) : ISupplierSource
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

    private static SupplierDto? ToSupplier(QuickbaseRecord record) =>
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
                record.Text(SuppliersTable.PaymentTerms),
                record.Text(SuppliersTable.MainEmail),
                record.User(SuppliersTable.LeadAssignedTo),
                record.Int(SuppliersTable.RelevantConsumerDistances),
                record.Text(SuppliersTable.InStockItemRecords),
                record.Int(SuppliersTable.TotalActivities),
                record.Decimal(SuppliersTable.TargetConsumerPrice),
                record.Int(SuppliersTable.DeliveredLast90Days),
                record.Int(SuppliersTable.DeliveredBefore90Days))
            : null;

    private static DataFreshness Freshness(QuickbaseQueryResult result) => new(result.Source.ToString(), result.FetchedAt);
}
