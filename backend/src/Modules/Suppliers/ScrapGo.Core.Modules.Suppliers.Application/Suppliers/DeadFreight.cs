using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// Whether a supplier is exempt from dead freight: a dropdown in the portal.
/// Serialized by name (<c>"Exempt"</c>); the label
/// (<see cref="DeadFreightCatalog.Label"/>) is what the dropdown shows. In
/// Quickbase it's a checkbox (Suppliers field 321): checked is
/// <see cref="NotExempt"/>, unchecked <see cref="Exempt"/>.
/// </summary>
/// <remarks>Never rename a value: the name is the API contract.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<DeadFreight>))]
public enum DeadFreight
{
    Exempt,
    NotExempt,
}

/// <summary>One dropdown option: the value the API sends and accepts, and the label to show.</summary>
public sealed record DeadFreightOption(DeadFreight Value, string Label);

/// <summary>The dead freight options and their labels, in dropdown order.</summary>
public static class DeadFreightCatalog
{
    /// <summary>Every option, in dropdown order.</summary>
    public static IReadOnlyList<DeadFreightOption> Options { get; } =
        [.. Enum.GetValues<DeadFreight>().Select(value => new DeadFreightOption(value, value.Label()))];

    /// <summary>The display label: <c>"Exempt"</c> or <c>"Not Exempt"</c>.</summary>
    public static string Label(this DeadFreight value) => value switch
    {
        DeadFreight.Exempt => "Exempt",
        DeadFreight.NotExempt => "Not Exempt",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Not a known dead freight value."),
    };
}
