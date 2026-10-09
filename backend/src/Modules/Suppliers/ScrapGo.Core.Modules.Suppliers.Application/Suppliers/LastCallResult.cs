using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// The result of the last call to a supplier (Quickbase Suppliers field 193,
/// dropdown). Serialized by name; labels are in <see cref="LastCallResults.Catalog"/>.
/// </summary>
/// <remarks>Append new values; never rename one, since the name is the API contract.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<LastCallResult>))]
public enum LastCallResult
{
    BadNumber,
    WrongBusiness,
    Consumer,
    MultipleLocation,
    NoAnswerVoiceMail,
    NoDecisionMaker,
    CantFillTruckLoad,
    NoEquipment,
    NoContactAfter4Calls,
    NotInterested,
    SendToTheGrid,
    Connected,
    ScrapHauls,
    PoPending,
}

public static class LastCallResults
{
    /// <summary>Labels as given for the Quickbase dropdown.</summary>
    public static DropdownCatalog<LastCallResult> Catalog { get; } = new(new Dictionary<LastCallResult, string>
    {
        [LastCallResult.BadNumber] = "Bad Number",
        [LastCallResult.WrongBusiness] = "Wrong Business",
        [LastCallResult.Consumer] = "Consumer",
        [LastCallResult.MultipleLocation] = "Multiple Location",
        [LastCallResult.NoAnswerVoiceMail] = "No Answer - Voice Mail",
        [LastCallResult.NoDecisionMaker] = "No Decision Maker",
        [LastCallResult.CantFillTruckLoad] = "Can't Fill Truck Load",
        [LastCallResult.NoEquipment] = "No Equipment",
        [LastCallResult.NoContactAfter4Calls] = "No Contact after 4 Calls",
        [LastCallResult.NotInterested] = "Not Interested",
        [LastCallResult.SendToTheGrid] = "Send to the grid",
        [LastCallResult.Connected] = "Connected",
        [LastCallResult.ScrapHauls] = "Scrap Hauls",
        [LastCallResult.PoPending] = "PO Pending",
    });
}
