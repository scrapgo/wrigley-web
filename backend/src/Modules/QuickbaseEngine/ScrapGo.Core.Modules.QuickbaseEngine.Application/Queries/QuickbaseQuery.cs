namespace ScrapGo.Core.Modules.QuickbaseEngine.Application.Queries;

/// <summary>
/// A Quickbase "query for data" request (<c>POST /v1/records/query</c>),
/// modelled as data so it can be hashed. Two queries with equal members hit
/// the same cache entry.
/// </summary>
/// <param name="TableId">The table to query (<c>from</c>), e.g. <c>"bck7gp3q2"</c>.</param>
/// <param name="Select">Field ids to return, in order. Null returns the table's default fields.</param>
/// <param name="Where">A Quickbase query string, e.g. <c>"{6.EX.'Open'}"</c>. Null returns every record.</param>
/// <param name="SortBy">Sort order. Null uses the table's default sort.</param>
/// <param name="Skip">Records to skip, for paging.</param>
/// <param name="Top">Maximum records to return, for paging.</param>
public sealed record QuickbaseQuery(
    string TableId,
    IReadOnlyList<int>? Select = null,
    string? Where = null,
    IReadOnlyList<QuickbaseSort>? SortBy = null,
    int? Skip = null,
    int? Top = null);

public sealed record QuickbaseSort(int FieldId, QuickbaseSortOrder Order = QuickbaseSortOrder.Ascending);

public enum QuickbaseSortOrder
{
    Ascending,
    Descending,
}
