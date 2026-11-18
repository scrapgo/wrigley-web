namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// The supplier Dashboard's stats (<c>GET /api/suppliers/{recordId}/dashboard-stats</c>).
/// Bound from <c>Suppliers:Dashboard</c>; the defaults need no configuration.
/// </summary>
public sealed class SupplierDashboardOptions
{
    public const string SectionName = "Suppliers:Dashboard";

    /// <summary>
    /// ITS Dispatch statuses (field 8) that count as a load. Only Delivered by
    /// default: Pending rows repeat delivered rows' PO numbers and would double count.
    /// </summary>
    /// <remarks>
    /// Empty here and defaulted in <see cref="EffectiveStatuses"/>: the configuration
    /// binder appends to a non-empty array instead of replacing it.
    /// </remarks>
    public string[] Statuses { get; set; } = [];

    public IReadOnlyList<string> EffectiveStatuses => Statuses is { Length: > 0 } statuses ? statuses : ["Delivered"];

    /// <summary>The time zone whose date is "today" for YTD and the 90-day buckets.</summary>
    public string BusinessTimeZone { get; set; } = "America/Chicago";
}
