namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>
/// Quickbase dropdown fields as enums: the stored text is the value's label
/// (<see cref="DropdownCatalog{TEnum}"/>). <see cref="ToQuickbase"/> for
/// writing, <see cref="TryFromQuickbase"/> for reading.
/// </summary>
public static class QuickbaseDropdowns
{
    public static string ToQuickbase<TEnum>(DropdownCatalog<TEnum> catalog, TEnum value)
        where TEnum : struct, Enum =>
        catalog.Label(value);

    /// <summary>False for empty text and for text that isn't one of the catalog's labels.</summary>
    public static bool TryFromQuickbase<TEnum>(DropdownCatalog<TEnum> catalog, string? text, out TEnum value)
        where TEnum : struct, Enum =>
        catalog.TryParseLabel(text, out value);
}
