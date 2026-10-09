using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// A supplier's objection (Quickbase Suppliers field 236, single-choice dropdown).
/// Serialized by name; labels are in <see cref="SupplierObjections.Catalog"/>.
/// </summary>
/// <remarks>Append new values; never rename one, since the name is the API contract.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<SupplierObjection>))]
public enum SupplierObjection
{
    Hot,
    Price,
    PaymentTerms,
    Netting,
    NotInterested,
    TooSmall,
    LaborIssues,
    NoEquipment,
    TooDifficult,
    PastScrapGoIssues,
    PastTruckerIssues,
    UsesOwnTruck,
    Nonferrous,
    Weird,
    IDontKnow,
    OtherFerrous,
    SellsToOurConsumer,
    IsAMobileCrusher,
    NoContact,
    Loyalty,
    Seasonal,
    TooFar,
}

public static class SupplierObjections
{
    /// <summary>The Quickbase dropdown's values, in order. A label must match Quickbase's text exactly.</summary>
    public static DropdownCatalog<SupplierObjection> Catalog { get; } = new(new Dictionary<SupplierObjection, string>
    {
        [SupplierObjection.Hot] = "Hot",
        [SupplierObjection.Price] = "Price",
        [SupplierObjection.PaymentTerms] = "Payment Terms",
        [SupplierObjection.Netting] = "Netting",
        [SupplierObjection.NotInterested] = "Not interested",
        [SupplierObjection.TooSmall] = "Too small",
        [SupplierObjection.LaborIssues] = "Labor Issues",
        [SupplierObjection.NoEquipment] = "No equipment",
        [SupplierObjection.TooDifficult] = "Too Difficult",
        [SupplierObjection.PastScrapGoIssues] = "Past ScrapGo Issues",
        [SupplierObjection.PastTruckerIssues] = "Past Trucker Issues",
        [SupplierObjection.UsesOwnTruck] = "Uses Own Truck",
        [SupplierObjection.Nonferrous] = "Nonferrous",
        [SupplierObjection.Weird] = "Weird",
        [SupplierObjection.IDontKnow] = "I Don't Know",
        [SupplierObjection.OtherFerrous] = "Other Ferrous",
        [SupplierObjection.SellsToOurConsumer] = "Sells to Our Consumer",
        [SupplierObjection.IsAMobileCrusher] = "Is a Mobile Crusher",
        [SupplierObjection.NoContact] = "No Contact",
        [SupplierObjection.Loyalty] = "Loyalty",
        [SupplierObjection.Seasonal] = "Seasonal",
        [SupplierObjection.TooFar] = "Too Far",
    });
}
