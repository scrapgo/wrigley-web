namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>
/// Payment terms as stored in Quickbase (Suppliers field 320, text): the
/// label of each <see cref="PaymentTerms"/> value, e.g. <c>"Net 5"</c>.
/// Use <see cref="ToQuickbase"/> when writing the field, and
/// <see cref="TryFromQuickbase"/> when reading it.
/// </summary>
public static class QuickbasePaymentTerms
{
    /// <summary>The text to write to field 320.</summary>
    public static string ToQuickbase(PaymentTerms terms) => terms.Label();

    /// <summary>
    /// The value stored in field 320. False for empty text and for text that
    /// isn't one of the known terms (the caller decides how to report it).
    /// </summary>
    public static bool TryFromQuickbase(string? text, out PaymentTerms terms) =>
        PaymentTermsCatalog.TryParseLabel(text, out terms);
}
