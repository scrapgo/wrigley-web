namespace ScrapGo.Core.Modules.Suppliers.Application.Abstractions;

/// <summary>
/// Where supplier records live: the Quickbase Suppliers table today
/// (Infrastructure's <c>QuickbaseSupplierSource</c>). Performs no
/// authorization; <see cref="SupplierService"/> checks the caller first.
/// </summary>
public interface ISupplierSource
{
    /// <summary>One supplier by its Record ID#, the only unique key; null value when there is none.</summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<SupplierDto?>> FindByRecordIdAsync(int recordId, CancellationToken cancellationToken);

    /// <summary>One supplier's call and prospect status by Record ID#; null value when there is no such supplier.</summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<SupplierCallProspectStatusDto?>> FindCallProspectStatusAsync(int recordId, CancellationToken cancellationToken);

    /// <summary>One supplier's yard capabilities by Record ID#; null value when there is no such supplier.</summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<SupplierYardCapabilitiesDto?>> FindYardCapabilitiesAsync(int recordId, CancellationToken cancellationToken);

    /// <summary>One supplier's Target Pricing — Progress Rail by Record ID#; null value when there is no such supplier.</summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<SupplierTargetPricingProgressRailDto?>> FindTargetPricingProgressRailAsync(int recordId, CancellationToken cancellationToken);

    /// <summary>Suppliers with a name, sorted by name, optionally filtered by a name substring, one page at a time.</summary>
    /// <exception cref="SupplierSourceUnavailableException">The source failed and nothing cached could be served.</exception>
    Task<Sourced<SupplierPage>> ListAsync(string? search, int skip, int top, CancellationToken cancellationToken);
}

/// <summary>A value plus how fresh it is (Quickbase data is served through a cache).</summary>
public sealed record Sourced<T>(T Value, DataFreshness Freshness);

/// <param name="TotalRecords">Every matching record, not just this page.</param>
public sealed record SupplierPage(IReadOnlyList<SupplierSummaryDto> Items, int TotalRecords);

/// <summary>The supplier source failed (e.g. Quickbase down) and no cached copy could stand in.</summary>
public sealed class SupplierSourceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
