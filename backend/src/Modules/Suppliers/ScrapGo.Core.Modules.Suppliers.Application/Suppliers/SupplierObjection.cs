using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// A supplier's objection (Quickbase Suppliers field 236, single-choice
/// dropdown). Each value is sent and accepted as its exact text
/// (<c>"Payment Terms"</c>), which is also the label and the Quickbase text.
/// </summary>
/// <remarks>Add a value at the end with its exact text. Never change the text of one: it is the API contract.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<SupplierObjection>))]
public enum SupplierObjection
{
    [JsonStringEnumMemberName("Hot")]
    Hot,

    [JsonStringEnumMemberName("Price")]
    Price,

    [JsonStringEnumMemberName("Payment Terms")]
    PaymentTerms,

    [JsonStringEnumMemberName("Netting")]
    Netting,

    [JsonStringEnumMemberName("Not interested")]
    NotInterested,

    [JsonStringEnumMemberName("Too small")]
    TooSmall,

    [JsonStringEnumMemberName("Labor Issues")]
    LaborIssues,

    [JsonStringEnumMemberName("No equipment")]
    NoEquipment,

    [JsonStringEnumMemberName("Too Difficult")]
    TooDifficult,

    [JsonStringEnumMemberName("Past ScrapGo Issues")]
    PastScrapGoIssues,

    [JsonStringEnumMemberName("Past Trucker Issues")]
    PastTruckerIssues,

    [JsonStringEnumMemberName("Uses Own Truck")]
    UsesOwnTruck,

    [JsonStringEnumMemberName("Nonferrous")]
    Nonferrous,

    [JsonStringEnumMemberName("Weird")]
    Weird,

    [JsonStringEnumMemberName("I Don't Know")]
    IDontKnow,

    [JsonStringEnumMemberName("Other Ferrous")]
    OtherFerrous,

    [JsonStringEnumMemberName("Sells to Our Consumer")]
    SellsToOurConsumer,

    [JsonStringEnumMemberName("Is a Mobile Crusher")]
    IsAMobileCrusher,

    [JsonStringEnumMemberName("No Contact")]
    NoContact,

    [JsonStringEnumMemberName("Loyalty")]
    Loyalty,

    [JsonStringEnumMemberName("Seasonal")]
    Seasonal,

    [JsonStringEnumMemberName("Too Far")]
    TooFar,
}

public static class SupplierObjections
{
    public static DropdownCatalog<SupplierObjection> Catalog { get; } = DropdownCatalog<SupplierObjection>.FromJsonNames();
}
