namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>The Quickbase Call Notes table (<c>br8tgw3tp</c>): a supplier's call notes, linked by Related Supplier.</summary>
public static class CallNotesTable
{
    public const string TableId = "br8tgw3tp";

    /// <summary>Quickbase's built-in Date Created (an ISO timestamp).</summary>
    public const int DateCreated = 1;
    public const int RecordId = 3;
    public const int Notes = 17;

    /// <summary>The supplier's Record ID# (Suppliers table field 3).</summary>
    public const int RelatedSupplier = 19;

    /// <summary>The fields, in the order the portal's query asks for them.</summary>
    public static readonly IReadOnlyList<int> Fields = [RecordId, DateCreated, Notes, RelatedSupplier];

    /// <summary><c>{19.EX.'17511'}</c>: every call note of this supplier.</summary>
    public static string BySupplier(int supplierRecordId) => $"{{{RelatedSupplier}.EX.'{supplierRecordId}'}}";
}
