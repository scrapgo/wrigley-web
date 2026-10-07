using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Application.Organizations;

public enum OrganizationReadOutcome
{
    Success,

    /// <summary>A page value out of range.</summary>
    InvalidRequest,

    NotFound,
}

public sealed record OrganizationDetailResult(OrganizationReadOutcome Outcome, OrganizationDetailDto? Organization = null);

public sealed record OrganizationMembersResult(OrganizationReadOutcome Outcome, PagedResult<OrganizationMemberDto>? Members = null);

/// <summary>
/// Reads within one organization: its detail and its members.
/// </summary>
/// <remarks>
/// Authorization happens before these methods run, on the route's
/// <c>{organizationId}</c>: the membership guard (active membership) and
/// <c>[RequirePermission(User.Read)]</c> in that organization. The queries
/// are still filtered to that organization, so nothing from another one can
/// come back.
/// </remarks>
public sealed class OrganizationReadService(IAuthorizationQueries authorization)
{
    /// <remarks>
    /// NotFound is defensive. Through the HTTP pipeline an unknown organization
    /// never gets here: the caller can't be a member of it, so the guard
    /// answers 403, as it does for any organization they don't belong to.
    /// </remarks>
    public async Task<OrganizationDetailResult> GetAsync(int organizationId, CancellationToken cancellationToken) =>
        await authorization.GetOrganizationDetailAsync(organizationId, cancellationToken) is { } organization
            ? new(OrganizationReadOutcome.Success, organization)
            : new(OrganizationReadOutcome.NotFound);

    /// <summary>
    /// Every role one member holds in this organization: organization-level
    /// roles and application grants, with expiry. Null (NotFound) if they
    /// aren't a member, so other users can't be probed.
    /// </summary>
    public Task<IReadOnlyList<AssignedRoleDto>?> ListMemberGrantsAsync(int organizationId, int userId, CancellationToken cancellationToken) =>
        authorization.ListMemberGrantsAsync(organizationId, userId, cancellationToken);

    public async Task<OrganizationMembersResult> ListMembersAsync(
        int organizationId, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var pageRequest = new PageRequest(page, pageSize);
        if (!pageRequest.IsValid)
        {
            return new(OrganizationReadOutcome.InvalidRequest);
        }

        return new(OrganizationReadOutcome.Success,
            await authorization.ListMembersAsync(organizationId, pageRequest, cancellationToken));
    }
}
