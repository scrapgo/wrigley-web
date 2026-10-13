namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>The Quickbase Contacts table (<c>br8imsxs2</c>): a supplier's contacts, linked by Related Supplier.</summary>
public static class ContactsTable
{
    public const string TableId = "br8imsxs2";

    public const int RecordId = 3;
    public const int Cell = 15;
    public const int Email = 17;
    public const int FirstName = 18;
    public const int LastName = 20;
    public const int Notes = 21;
    public const int Title = 25;

    /// <summary>The supplier's Record ID# (Suppliers table field 3).</summary>
    public const int RelatedSupplier = 29;
    public const int Phone = 22;

    /// <summary>"Main?": the supplier's main contact (checkbox).</summary>
    public const int IsMain = 46;

    /// <summary>The fields, in the order the portal's query asks for them.</summary>
    public static readonly IReadOnlyList<int> Fields =
        [RecordId, RelatedSupplier, LastName, FirstName, Title, Phone, Cell, Email, Notes, IsMain];

    /// <summary><c>{29.EX.'17511'}</c>: every contact of this supplier.</summary>
    public static string BySupplier(int supplierRecordId) => $"{{{RelatedSupplier}.EX.'{supplierRecordId}'}}";
}
