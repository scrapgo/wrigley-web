namespace ScrapGo.Core.Modules.Suppliers.Api.Suppliers;

/// <summary>The <c>status</c> filter's values for Swagger: the same list as <c>GET …/linked-its-dispatch-records/status-filters</c>.</summary>
public sealed class DispatchStatusFilterValues : IOpenApiAllowedValues
{
    public IReadOnlyList<string> Values { get; } = [.. DispatchStatuses.FilterOptions.Select(option => option.Label)];
}
