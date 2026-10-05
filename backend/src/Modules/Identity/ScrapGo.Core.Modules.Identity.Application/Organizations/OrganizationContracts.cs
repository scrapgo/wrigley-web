using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Organizations;

/// <summary>Filters for the platform-wide organization list. Every filter is optional.</summary>
/// <param name="Search">Case-insensitive substring of the name or slug.</param>
public sealed record OrganizationListFilter(string? Search = null, OrganizationStatus? Status = null);

/// <summary>One organization, as its own administrators see it.</summary>
/// <param name="ActiveMemberCount">Members whose membership is active.</param>
public sealed record OrganizationDetailDto(
    int Id,
    string Name,
    string Slug,
    string Status,
    DateTimeOffset CreatedAt,
    int ActiveMemberCount);

/// <summary>One member of an organization, as an organization admin sees them.</summary>
/// <param name="DisplayName">Always null for now; see <see cref="UserSummaryDto"/>.</param>
/// <param name="Roles">The member's active roles in this organization only.</param>
/// <param name="JoinedAt">When the membership was created.</param>
public sealed record OrganizationMemberDto(
    int UserId,
    string Email,
    string? DisplayName,
    IReadOnlyList<AssignedRoleDto> Roles,
    DateTimeOffset JoinedAt);

/// <param name="ActorUid">The caller's UID from the validated token's <c>sub</c> claim.</param>
/// <param name="OrganizationId">From the route.</param>
/// <param name="Name">From the request body.</param>
public sealed record UpdateOrganizationCommand(string ActorUid, int OrganizationId, string? Name)
{
    /// <summary>
    /// The InvalidRequest check a handler runs before anything else. The same
    /// rule as creating an organization: the name must contain a letter or digit.
    /// </summary>
    public bool IsWellFormed => Organization.ToSlug(Name).Length > 0;
}
