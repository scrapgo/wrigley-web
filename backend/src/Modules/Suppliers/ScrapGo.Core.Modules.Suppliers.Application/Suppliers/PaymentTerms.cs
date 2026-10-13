using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// A supplier's payment terms: a fixed list, shown as a dropdown in the
/// portal. Each value is sent and accepted as its exact text (<c>"Net 5"</c>),
/// which is also the label and the text stored in Quickbase (Suppliers field 320).
/// </summary>
/// <remarks>Add a value at the end with its exact text. Never change the text of one: it is the API contract.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<PaymentTerms>))]
public enum PaymentTerms
{
    [JsonStringEnumMemberName("Net 5")]
    Net5,

    [JsonStringEnumMemberName("Net 10")]
    Net10,

    [JsonStringEnumMemberName("Net 30")]
    Net30,

    [JsonStringEnumMemberName("Tuesday/Thursday")]
    TuesdayThursday,

    [JsonStringEnumMemberName("ML Norwood")]
    MlNorwood,
}

/// <summary>One dropdown option: the value the API sends and accepts, and the label to show (the same text).</summary>
public sealed record PaymentTermsOption(PaymentTerms Value, string Label);

/// <summary>The payment terms list and its labels, in dropdown order.</summary>
public static class PaymentTermsCatalog
{
    private static readonly DropdownCatalog<PaymentTerms> Catalog = DropdownCatalog<PaymentTerms>.FromJsonNames();

    /// <summary>Every option, in dropdown order.</summary>
    public static IReadOnlyList<PaymentTermsOption> Options { get; } =
        [.. Catalog.Options.Select(option => new PaymentTermsOption(option.Value, option.Label))];

    /// <summary>The exact text, e.g. <c>"Net 5"</c>: the JSON value, the label and the Quickbase text.</summary>
    public static string Label(this PaymentTerms terms) => Catalog.Label(terms);

    /// <summary>
    /// The value whose text matches <paramref name="text"/>, ignoring case and
    /// extra whitespace (<c>" net  5 "</c> is <see cref="PaymentTerms.Net5"/>).
    /// False for empty or unknown text.
    /// </summary>
    public static bool TryParseLabel(string? text, out PaymentTerms terms) => Catalog.TryParseLabel(text, out terms);
}
