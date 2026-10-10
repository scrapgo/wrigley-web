using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// The result of the last call to a supplier (Quickbase Suppliers field 193,
/// dropdown). Each value is sent and accepted as its exact text
/// (<c>"No Answer - Voice Mail"</c>), which is also the label and the Quickbase text.
/// </summary>
/// <remarks>Add a value at the end with its exact text. Never change the text of one: it is the API contract.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<LastCallResult>))]
public enum LastCallResult
{
    [JsonStringEnumMemberName("Bad Number")]
    BadNumber,

    [JsonStringEnumMemberName("Wrong Business")]
    WrongBusiness,

    [JsonStringEnumMemberName("Consumer")]
    Consumer,

    [JsonStringEnumMemberName("Multiple Location")]
    MultipleLocation,

    [JsonStringEnumMemberName("No Answer - Voice Mail")]
    NoAnswerVoiceMail,

    [JsonStringEnumMemberName("No Decision Maker")]
    NoDecisionMaker,

    [JsonStringEnumMemberName("Can't Fill Truck Load")]
    CantFillTruckLoad,

    [JsonStringEnumMemberName("No Equipment")]
    NoEquipment,

    [JsonStringEnumMemberName("No Contact after 4 Calls")]
    NoContactAfter4Calls,

    [JsonStringEnumMemberName("Not Interested")]
    NotInterested,

    [JsonStringEnumMemberName("Send to the grid")]
    SendToTheGrid,

    [JsonStringEnumMemberName("Connected")]
    Connected,

    [JsonStringEnumMemberName("Scrap Hauls")]
    ScrapHauls,

    [JsonStringEnumMemberName("PO Pending")]
    PoPending,
}

public static class LastCallResults
{
    public static DropdownCatalog<LastCallResult> Catalog { get; } = DropdownCatalog<LastCallResult>.FromJsonNames();
}
