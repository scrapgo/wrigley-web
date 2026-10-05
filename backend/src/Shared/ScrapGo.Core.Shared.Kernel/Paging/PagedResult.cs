namespace ScrapGo.Core.Shared.Kernel.Paging;

/// <summary>One page of a list endpoint's results, in a stable order (by id unless the endpoint says otherwise).</summary>
/// <param name="Page">1-based, as requested.</param>
/// <param name="TotalCount">Matching rows across every page, after filters.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResult<T> Create(IReadOnlyList<T> items, PageRequest request, int totalCount) =>
        new(items, request.ResolvedPage, request.ResolvedPageSize, totalCount);
}
