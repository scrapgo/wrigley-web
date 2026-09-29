namespace ScrapGo.Core.Modules.Identity.Application.Organizations;

/// <summary>
/// The caller's organizations: active organizations only, and only where the
/// caller's membership is active. Disabled memberships and soft-deleted
/// organizations are excluded.
/// </summary>
public sealed class ListMyOrganizationsHandler(IUserRepository users, IAuthorizationQueries queries)
{
    public async Task<IReadOnlyList<OrganizationSummaryDto>> HandleAsync(string identityPlatformUid, CancellationToken cancellationToken) =>
        await users.GetIdByUidAsync(identityPlatformUid, cancellationToken) is { } userId
            ? await queries.ListActiveOrganizationsAsync(userId, cancellationToken)
            : [];
}
