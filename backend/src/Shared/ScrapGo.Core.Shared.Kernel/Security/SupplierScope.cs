namespace ScrapGo.Core.Shared.Kernel.Security;

/// <summary>
/// Which Quickbase suppliers the caller may see: every supplier, or only the
/// listed Record IDs (possibly none).
/// </summary>
public sealed class SupplierScope
{
    private SupplierScope(bool isAll, IReadOnlySet<int> recordIds)
    {
        IsAll = isAll;
        RecordIds = recordIds;
    }

    /// <summary>Every supplier (<c>suppliers.view_all</c>).</summary>
    public static SupplierScope All { get; } = new(true, new HashSet<int>());

    /// <summary>No supplier at all.</summary>
    public static SupplierScope None { get; } = new(false, new HashSet<int>());

    public bool IsAll { get; }

    /// <summary>The supplier Record IDs in scope when <see cref="IsAll"/> is false; empty otherwise.</summary>
    public IReadOnlySet<int> RecordIds { get; }

    /// <summary>True when the scope can't contain any supplier.</summary>
    public bool IsEmpty => !IsAll && RecordIds.Count == 0;

    /// <summary>Only these Record IDs (<c>suppliers.view_own</c>: the caller's linked suppliers).</summary>
    public static SupplierScope Of(IEnumerable<int> recordIds)
    {
        var ids = recordIds.ToHashSet();
        return ids.Count == 0 ? None : new SupplierScope(false, ids);
    }

    public bool Includes(int supplierRecordId) => IsAll || RecordIds.Contains(supplierRecordId);

    public override string ToString() => IsAll ? "All" : $"[{string.Join(", ", RecordIds.Order())}]";
}
