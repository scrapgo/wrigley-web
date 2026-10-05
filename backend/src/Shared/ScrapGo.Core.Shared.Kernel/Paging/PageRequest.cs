namespace ScrapGo.Core.Shared.Kernel.Paging;

/// <summary>
/// Offset paging parameters for list endpoints, bound from the
/// <c>page</c> (1-based) and <c>pageSize</c> query parameters. Both are
/// optional; an omitted value takes its default.
/// </summary>
/// <remarks>
/// Nullable so the framework never rejects a request on its own: a handler
/// checks <see cref="IsValid"/> first and maps a failure to its InvalidRequest
/// outcome (400 <c>invalid_request</c>), like every other request in this API.
/// </remarks>
public sealed record PageRequest(int? Page = null, int? PageSize = null)
{
    public const int DefaultPageSize = 25;

    public const int MaxPageSize = 100;

    /// <summary>Caps the offset (<see cref="Skip"/>) well inside <see cref="int"/>.</summary>
    public const int MaxPage = 1_000_000;

    public bool IsValid =>
        Page is null or (>= 1 and <= MaxPage)
        && PageSize is null or (>= 1 and <= MaxPageSize);

    public int ResolvedPage => Page ?? 1;

    public int ResolvedPageSize => PageSize ?? DefaultPageSize;

    /// <summary>Rows to skip. Only meaningful when <see cref="IsValid"/>.</summary>
    public int Skip => (ResolvedPage - 1) * ResolvedPageSize;
}
