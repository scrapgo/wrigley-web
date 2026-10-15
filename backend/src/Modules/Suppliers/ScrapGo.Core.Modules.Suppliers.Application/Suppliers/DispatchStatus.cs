using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// An ITS dispatch record's status (Quickbase ITS Dispatch Records field 8,
/// dropdown), sent and accepted as its exact text.
/// </summary>
/// <remarks>Add a value at the end with its exact text. Never change the text of one: it is the API contract.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<DispatchStatus>))]
public enum DispatchStatus
{
    [JsonStringEnumMemberName("Pending")]
    Pending,

    [JsonStringEnumMemberName("Delivered")]
    Delivered,

    [JsonStringEnumMemberName("Open")]
    Open,

    [JsonStringEnumMemberName("On Route")]
    OnRoute,

    [JsonStringEnumMemberName("Completed")]
    Completed,

    [JsonStringEnumMemberName("Dispatched")]
    Dispatched,

    [JsonStringEnumMemberName("Refused")]
    Refused,

    [JsonStringEnumMemberName("Invoiced")]
    Invoiced,

    [JsonStringEnumMemberName("Test")]
    Test,

    [JsonStringEnumMemberName("Test2")]
    Test2,

    [JsonStringEnumMemberName("Loading")]
    Loading,

    [JsonStringEnumMemberName("Covered")]
    Covered,

    [JsonStringEnumMemberName("In Yard")]
    InYard,

    [JsonStringEnumMemberName("Unloading")]
    Unloading,

    [JsonStringEnumMemberName("Roll Over")]
    RollOver,
}

public static class DispatchStatuses
{
    public static DropdownCatalog<DispatchStatus> Catalog { get; } = DropdownCatalog<DispatchStatus>.FromJsonNames();
}
