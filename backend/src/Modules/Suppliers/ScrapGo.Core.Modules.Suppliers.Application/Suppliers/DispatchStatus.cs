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

    /// <summary>The statuses offered in the portal's status filter: every one but the Test statuses.</summary>
    public static IReadOnlyList<DropdownOption<DispatchStatus>> FilterOptions { get; } =
        [.. Catalog.Options.Where(option => option.Value is not (DispatchStatus.Test or DispatchStatus.Test2))];

    /// <summary>
    /// Reads a status filter (each value the status's exact text, e.g. "On Route") into
    /// Quickbase text; null when any value is not a known status. No values is an empty list.
    /// </summary>
    public static IReadOnlyList<string>? ParseFilter(IReadOnlyCollection<string>? values)
    {
        var statuses = new List<string>();
        foreach (var value in values ?? [])
        {
            if (!Catalog.TryParseLabel(value, out var status))
            {
                return null;
            }

            var label = Catalog.Label(status);
            if (!statuses.Contains(label))
            {
                statuses.Add(label);
            }
        }

        return statuses;
    }
}
