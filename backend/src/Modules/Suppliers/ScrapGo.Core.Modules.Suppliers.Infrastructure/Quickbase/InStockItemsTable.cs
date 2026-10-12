namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>The Quickbase In-Stock Items table (<c>bscpxjrw3</c>): items a supplier has, linked by Related Supplier.</summary>
public static class InStockItemsTable
{
    public const string TableId = "bscpxjrw3";

    public const int RecordId = 3;
    public const int MaterialType = 6;
    public const int Weight = 7;
    public const int UnitOfMeasure = 8;

    /// <summary>Packaging / Prep: one field, shown under both names.</summary>
    public const int Packaging = 10;
    public const int TargetOffer = 14;

    /// <summary>The supplier's Record ID# (Suppliers table field 3).</summary>
    public const int RelatedSupplier = 15;
    public const int TruckType = 18;
    public const int Volume = 19;
    public const int TrucksPerWeek = 20;

    /// <summary>
    /// The fields, exactly as the portal's Materials Pricing query asks for them.
    /// Field 10 is listed twice (Packaging and Prep), as in the original query.
    /// </summary>
    public static readonly IReadOnlyList<int> Fields =
        [RecordId, RelatedSupplier, MaterialType, Packaging, Packaging, Weight, UnitOfMeasure, TargetOffer, TruckType, TrucksPerWeek, Volume];

    /// <summary><c>{15.EX.'17511'}</c>: every item of this supplier.</summary>
    public static string BySupplier(int supplierRecordId) => $"{{{RelatedSupplier}.EX.'{supplierRecordId}'}}";
}
