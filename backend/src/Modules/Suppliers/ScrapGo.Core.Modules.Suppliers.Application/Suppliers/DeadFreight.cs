using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// Whether a supplier is exempt from dead freight: a dropdown in the portal.
/// Each value is sent and accepted as its exact text (<c>"Not Exempt"</c>),
/// which is also the label. In Quickbase it's a checkbox (Suppliers field 321):
/// checked is <see cref="NotExempt"/>, unchecked <see cref="Exempt"/>.
/// </summary>
/// <remarks>Never change the text of a value: it is the API contract.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<DeadFreight>))]
public enum DeadFreight
{
    [JsonStringEnumMemberName("Exempt")]
    Exempt,

    [JsonStringEnumMemberName("Not Exempt")]
    NotExempt,
}

/// <summary>One dropdown option: the value the API sends and accepts, and the label to show (the same text).</summary>
public sealed record DeadFreightOption(DeadFreight Value, string Label);

/// <summary>The dead freight options and their labels, in dropdown order.</summary>
public static class DeadFreightCatalog
{
    private static readonly DropdownCatalog<DeadFreight> Catalog = DropdownCatalog<DeadFreight>.FromJsonNames();

    /// <summary>Every option, in dropdown order.</summary>
    public static IReadOnlyList<DeadFreightOption> Options { get; } =
        [.. Catalog.Options.Select(option => new DeadFreightOption(option.Value, option.Label))];

    /// <summary>The exact text: <c>"Exempt"</c> or <c>"Not Exempt"</c>.</summary>
    public static string Label(this DeadFreight value) => Catalog.Label(value);
}
