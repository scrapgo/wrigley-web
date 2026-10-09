using System.Globalization;

namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>
/// One row of a Quickbase <c>records/query</c> response: <c>{ "8": { "value": ... } }</c>
/// keyed by field id. Reads tolerate missing fields and nulls.
/// </summary>
internal readonly struct QuickbaseRecord(JsonElement row)
{
    public string? Text(int fieldId) => Value(fieldId) is { } value
        ? value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        }
        : null;

    public decimal? Decimal(int fieldId) => Value(fieldId) is { } value
        ? value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        }
        : null;

    public int? Int(int fieldId) => Decimal(fieldId) is { } number ? (int)decimal.Truncate(number) : null;

    /// <summary>
    /// A date or date/time field: ISO 8601 text (<c>"2026-10-15"</c> or
    /// <c>"2026-10-15T14:00:00Z"</c>), read as UTC.
    /// </summary>
    public DateTimeOffset? DateTime(int fieldId) =>
        Text(fieldId) is { Length: > 0 } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    /// <summary>A dropdown's text: a single value, or the first of a multi-select.</summary>
    public string? Choice(int fieldId) => Text(fieldId) ?? TextList(fieldId).FirstOrDefault();

    /// <summary>A checkbox field: true or false; null only when the field is missing or empty.</summary>
    public bool? Bool(int fieldId) => Value(fieldId) is { } value
        ? value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            JsonValueKind.Number when value.TryGetInt32(out var number) => number != 0,
            _ => null,
        }
        : null;

    /// <summary>A multi-select text field (array of strings); a single string becomes one item.</summary>
    public IReadOnlyList<string> TextList(int fieldId) => Value(fieldId) is { } value
        ? value.ValueKind switch
        {
            JsonValueKind.Array => [.. value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!)],
            JsonValueKind.String when value.GetString() is { Length: > 0 } single => [single],
            _ => [],
        }
        : [];

    /// <summary>A user field: <c>{ "id", "email", "name" }</c>.</summary>
    public SupplierUserDto? User(int fieldId) => Value(fieldId) is { ValueKind: JsonValueKind.Object } value
        ? new SupplierUserDto(Property(value, "id"), Property(value, "email"), Property(value, "name"))
        : null;

    private JsonElement? Value(int fieldId) =>
        row.TryGetProperty(fieldId.ToString(CultureInfo.InvariantCulture), out var field)
        && field.TryGetProperty("value", out var value)
        && value.ValueKind != JsonValueKind.Null
            ? value
            : null;

    private static string? Property(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
}
