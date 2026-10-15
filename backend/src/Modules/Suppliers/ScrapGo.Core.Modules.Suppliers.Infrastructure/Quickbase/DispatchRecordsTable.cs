namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>
/// The Quickbase ITS Dispatch Records table (<c>bspsb9kpm</c>): loads, linked
/// to a supplier by field 366.
/// </summary>
public static class DispatchRecordsTable
{
    public const string TableId = "bspsb9kpm";

    public const int RecordId = 3;
    public const int LoadNumber = 6;
    public const int Status = 8;
    public const int ShipDate = 14;
    public const int DeliveryDate = 15;
    public const int CarrierDriver = 28;
    public const int Consumer = 241;

    /// <summary>The supplier's Record ID# (Suppliers table field 3).</summary>
    public const int RelatedSupplier = 366;
    public const int Weight = 378;
    public const int ConsumerCity = 432;
    public const int ConsumerState = 433;
    public const int Material = 508;

    /// <summary>
    /// The portal's query, with field 6 (Load #) added so the load number can be
    /// shown: the original select omitted it.
    /// </summary>
    public static readonly IReadOnlyList<int> Fields =
        [RecordId, LoadNumber, Consumer, ConsumerCity, ConsumerState, CarrierDriver, Material, Weight, ShipDate, DeliveryDate, Status];

    /// <summary><c>{366.EX.'17511'}</c>: every dispatch record linked to this supplier.</summary>
    public static string BySupplier(int supplierRecordId) => $"{{{RelatedSupplier}.EX.'{supplierRecordId}'}}";

    /// <summary>
    /// "Consumer - City, State". Empty parts are left out: "Consumer - State",
    /// "Consumer", "City, State"; null when all three are empty.
    /// </summary>
    public static string? FormatConsumer(string? consumer, string? city, string? state)
    {
        var place = string.Join(", ", new[] { city, state }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
        var name = consumer?.Trim();

        return (string.IsNullOrEmpty(name), place.Length == 0) switch
        {
            (false, false) => $"{name} - {place}",
            (false, true) => name,
            (true, false) => place,
            _ => null,
        };
    }
}
