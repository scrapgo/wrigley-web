using System.Reflection;
using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>One dropdown option: the value the API sends and accepts, and the label to show.</summary>
public sealed record DropdownOption<TEnum>(TEnum Value, string Label)
    where TEnum : struct, Enum;

/// <summary>
/// A Quickbase dropdown as an enum. Each value's label is its exact text: the
/// JSON value the API sends and accepts, what the portal shows, and what
/// Quickbase stores. Reading Quickbase text ignores case, extra whitespace and
/// curly apostrophes.
/// </summary>
public sealed class DropdownCatalog<TEnum>
    where TEnum : struct, Enum
{
    private readonly IReadOnlyDictionary<TEnum, string> _labels;

    /// <exception cref="ArgumentException">A value of <typeparamref name="TEnum"/> has no label.</exception>
    public DropdownCatalog(IReadOnlyDictionary<TEnum, string> labels)
    {
        var missing = Enum.GetValues<TEnum>().Where(value => !labels.ContainsKey(value)).ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException($"{typeof(TEnum).Name} values without a label: {string.Join(", ", missing)}.", nameof(labels));
        }

        _labels = labels;
        Options = [.. Enum.GetValues<TEnum>().Select(value => new DropdownOption<TEnum>(value, labels[value]))];
    }

    /// <summary>
    /// The catalog of an enum whose members carry their exact text as
    /// <see cref="JsonStringEnumMemberNameAttribute"/>, the single source of each label.
    /// </summary>
    /// <exception cref="InvalidOperationException">A member has no <see cref="JsonStringEnumMemberNameAttribute"/>.</exception>
    public static DropdownCatalog<TEnum> FromJsonNames() =>
        new(Enum.GetValues<TEnum>().ToDictionary(
            value => value,
            value => typeof(TEnum).GetField(value.ToString())?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
                ?? throw new InvalidOperationException($"{typeof(TEnum).Name}.{value} has no [JsonStringEnumMemberName].")));

    /// <summary>Every option, in enum (dropdown) order.</summary>
    public IReadOnlyList<DropdownOption<TEnum>> Options { get; }

    /// <summary>The label, which is also the Quickbase text.</summary>
    public string Label(TEnum value) =>
        _labels.TryGetValue(value, out var label)
            ? label
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"Not a known {typeof(TEnum).Name}.");

    /// <summary>The value whose label matches <paramref name="text"/>; false for empty or unknown text.</summary>
    public bool TryParseLabel(string? text, out TEnum value)
    {
        var normalized = Normalize(text);
        foreach (var (candidate, label) in _labels)
        {
            if (normalized.Length > 0 && string.Equals(Normalize(label), normalized, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string Normalize(string? text) =>
        string.Join(' ', (text ?? string.Empty).Replace('\u2019', '\'').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
