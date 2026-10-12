namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>The Quickbase In-Stock Items table (<c>bscpxjrw3</c>): items a supplier has, linked by Related Supplier.</summary>
public static class InStockItemsTable
{
    public const string TableId = "bscpxjrw3";

    public const int RecordId = 3;
    public const int MaterialType = 6;
    public const int Weight = 7;
    public const int UnitOfMeasure = 8;
    public const int WeightInLbs = 9;
    public const int Preparation = 10;
    public const int Availability = 13;
    public const int TargetOffer = 14;

    /// <summary>The supplier's Record ID# (Suppliers table field 3).</summary>
    public const int RelatedSupplier = 15;
    public const int SupplierAccounting = 16;
    public const int TruckType = 18;
    public const int Volume = 19;
    public const int TrucksPerWeek = 20;
    public const int FreightCost = 21;
    public const int Verified = 22;
    public const int DirtyOrClean = 23;
    public const int Notes = 26;
    public const int VolumeUom = 27;
    public const int SupplierInStockItemRecords = 28;
    public const int OfferStatus = 31;

    /// <summary>The fields, in the order the portal's query asks for them.</summary>
    public static readonly IReadOnlyList<int> Fields =
    [
        RecordId, RelatedSupplier, SupplierAccounting, MaterialType, Weight, UnitOfMeasure, WeightInLbs, Preparation,
        Availability, TargetOffer, TruckType, Volume, TrucksPerWeek, FreightCost, Verified, DirtyOrClean, Notes,
        VolumeUom, SupplierInStockItemRecords, OfferStatus,
    ];

    /// <summary><c>{15.EX.'17511'}</c>: every item of this supplier.</summary>
    public static string BySupplier(int supplierRecordId) => $"{{{RelatedSupplier}.EX.'{supplierRecordId}'}}";
}
