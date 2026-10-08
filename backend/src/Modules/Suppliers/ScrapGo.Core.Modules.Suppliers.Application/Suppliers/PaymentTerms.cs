using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// A supplier's payment terms: a fixed list, shown as a dropdown in the
/// portal. Serialized by name (<c>"Net5"</c>); each value's label
/// (<see cref="PaymentTermsCatalog.Label"/>) is what the dropdown shows and
/// the exact text stored in Quickbase (Suppliers field 320).
/// </summary>
/// <remarks>Append new values; never rename one, since the name is the API contract.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<PaymentTerms>))]
public enum PaymentTerms
{
    Net5,
    Net10,
    Net30,
    TuesdayThursday,
    MlNorwood,
}

/// <summary>One dropdown option: the value the API sends and accepts, and the label to show.</summary>
public sealed record PaymentTermsOption(PaymentTerms Value, string Label);

/// <summary>The payment terms list and its labels, in dropdown order.</summary>
public static class PaymentTermsCatalog
{
    private static readonly IReadOnlyDictionary<PaymentTerms, string> Labels = new Dictionary<PaymentTerms, string>
    {
        [PaymentTerms.Net5] = "Net 5",
        [PaymentTerms.Net10] = "Net 10",
        [PaymentTerms.Net30] = "Net 30",
        [PaymentTerms.TuesdayThursday] = "Tuesday/Thursday",
        [PaymentTerms.MlNorwood] = "ML Norwood",
    };

    /// <summary>Every option, in dropdown order.</summary>
    public static IReadOnlyList<PaymentTermsOption> Options { get; } =
        [.. Enum.GetValues<PaymentTerms>().Select(terms => new PaymentTermsOption(terms, Labels[terms]))];

    /// <summary>The display label, which is also the Quickbase text, e.g. <c>"Net 5"</c>.</summary>
    public static string Label(this PaymentTerms terms) =>
        Labels.TryGetValue(terms, out var label)
            ? label
            : throw new ArgumentOutOfRangeException(nameof(terms), terms, "Not a known payment terms value.");

    /// <summary>
    /// The value whose label matches <paramref name="text"/>, ignoring case and
    /// extra whitespace (<c>" net  5 "</c> is <see cref="PaymentTerms.Net5"/>).
    /// False for empty or unknown text.
    /// </summary>
    public static bool TryParseLabel(string? text, out PaymentTerms terms)
    {
        var normalized = Normalize(text);
        foreach (var (value, label) in Labels)
        {
            if (normalized.Length > 0 && string.Equals(Normalize(label), normalized, StringComparison.OrdinalIgnoreCase))
            {
                terms = value;
                return true;
            }
        }

        terms = default;
        return false;
    }

    private static string Normalize(string? text) =>
        string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
