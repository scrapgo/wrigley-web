namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>
/// Dead freight as stored in Quickbase (Suppliers field 321, checkbox):
/// checked is <see cref="DeadFreight.NotExempt"/>, unchecked
/// <see cref="DeadFreight.Exempt"/>. Use <see cref="ToQuickbase"/> when
/// writing the field, and <see cref="FromQuickbase"/> when reading it.
/// </summary>
public static class QuickbaseDeadFreight
{
    /// <summary>The checkbox value to write to field 321.</summary>
    public static bool ToQuickbase(DeadFreight value) => value switch
    {
        DeadFreight.Exempt => false,
        DeadFreight.NotExempt => true,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Not a known dead freight value."),
    };

    /// <summary>The value of field 321; null when Quickbase sent none.</summary>
    public static DeadFreight? FromQuickbase(bool? isChecked) => isChecked switch
    {
        true => DeadFreight.NotExempt,
        false => DeadFreight.Exempt,
        null => null,
    };
}
