using Microsoft.Extensions.Logging;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

public enum SupplierOutcome
{
    Success,

    /// <summary>The caller lacks <see cref="SupplierPermissions.Read"/> for this organization's application.</summary>
    Forbidden,

    NotFound,

    /// <summary>A paging value out of range.</summary>
    InvalidRequest,

    /// <summary>Quickbase failed and nothing cached could be served.</summary>
    SourceUnavailable,
}

public sealed record SupplierResult(SupplierOutcome Outcome, SupplierResponse? Supplier = null);

public sealed record SupplierListResult(SupplierOutcome Outcome, SupplierListResponse? Page = null);

/// <summary>
/// Whose supplier access to check: an organization's application
/// (<see cref="SupplierPermissions.Read"/>), or the platform when both are null
/// (<see cref="SupplierPermissions.PlatformRead"/>).
/// </summary>
public sealed record SupplierScope(int? OrganizationId, int? ApplicationId)
{
    public static readonly SupplierScope Platform = new(null, null);

    public static SupplierScope ForApplication(int organizationId, int applicationId) => new(organizationId, applicationId);
}

/// <param name="Search">Optional name substring (case-insensitive in Quickbase).</param>
public sealed record ListSuppliersQuery(SupplierScope Scope, string? Search, int? Skip, int? Top);

/// <summary>
/// Supplier reads for the portal. Every call checks the caller through
/// <see cref="IUserContext"/> before any Quickbase data is touched:
/// <see cref="SupplierPermissions.Read"/> at application scope, or
/// <see cref="SupplierPermissions.PlatformRead"/> at platform scope.
/// </summary>
/// <remarks>
/// On application routes the membership guard has already validated the
/// organization and application (membership, organization active, application
/// assigned); the permission check adds the grant and the Suppliers module
/// being enabled.
/// </remarks>
public sealed class SupplierService(IUserContext userContext, ISupplierSource source, ILogger<SupplierService> logger)
{
    public const int DefaultTop = 100;

    public const int MaxTop = 1000;

    public async Task<SupplierResult> GetAsync(SupplierScope scope, int recordId, CancellationToken cancellationToken)
    {
        if (!await CanReadAsync(scope, cancellationToken))
        {
            return new(SupplierOutcome.Forbidden);
        }

        if (recordId < 1)
        {
            return new(SupplierOutcome.NotFound);
        }

        try
        {
            var found = await source.FindByRecordIdAsync(recordId, cancellationToken);
            return found.Value is { } supplier
                ? new(SupplierOutcome.Success, new SupplierResponse(supplier, found.Freshness))
                : new(SupplierOutcome.NotFound);
        }
        catch (SupplierSourceUnavailableException ex)
        {
            logger.LogWarning(ex, "Supplier {RecordId} could not be read from the supplier source.", recordId);
            return new(SupplierOutcome.SourceUnavailable);
        }
    }

    public async Task<SupplierListResult> ListAsync(ListSuppliersQuery query, CancellationToken cancellationToken)
    {
        if (!await CanReadAsync(query.Scope, cancellationToken))
        {
            return new(SupplierOutcome.Forbidden);
        }

        var skip = query.Skip ?? 0;
        var top = query.Top ?? DefaultTop;
        if (skip < 0 || top < 1 || top > MaxTop)
        {
            return new(SupplierOutcome.InvalidRequest);
        }

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        try
        {
            var page = await source.ListAsync(search, skip, top, cancellationToken);
            return new(SupplierOutcome.Success, new SupplierListResponse(
                page.Value.Items, skip, top, page.Value.TotalRecords, page.Freshness));
        }
        catch (SupplierSourceUnavailableException ex)
        {
            logger.LogWarning(ex, "The supplier list could not be read from the supplier source.");
            return new(SupplierOutcome.SourceUnavailable);
        }
    }

    private Task<bool> CanReadAsync(SupplierScope scope, CancellationToken cancellationToken) =>
        scope is { OrganizationId: { } organizationId, ApplicationId: { } applicationId }
            ? userContext.HasApplicationPermissionAsync(SupplierPermissions.Read, organizationId, applicationId, cancellationToken)
            : userContext.HasPermissionAsync(SupplierPermissions.PlatformRead, organizationId: null, cancellationToken);
}
