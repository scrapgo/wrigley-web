namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>One role a user holds, in one scope.</summary>
/// <param name="OrganizationId">The organization the assignment is scoped to, or null for a platform-scoped assignment.</param>
/// <param name="ApplicationId">Set for an application grant (inside <paramref name="OrganizationId"/>).</param>
/// <param name="ExpiresAt">When the grant stops resolving; null never expires.</param>
public sealed record AssignedRoleDto(
    int RoleId, string Name, int? OrganizationId, int? ApplicationId = null, DateTimeOffset? ExpiresAt = null);

/// <summary>A user's effective permissions in exactly one scope.</summary>
/// <param name="OrganizationId">The organization, or null for platform scope.</param>
/// <param name="Permissions">Permission names, ordered by name.</param>
/// <param name="ApplicationId">Set for an application scope inside <paramref name="OrganizationId"/>.</param>
public sealed record ScopedPermissionsDto(int? OrganizationId, IReadOnlyList<string> Permissions, int? ApplicationId = null);

/// <summary>Filters for the admin user list. Every filter is optional; omitted means "any".</summary>
/// <param name="Search">Case-insensitive substring of the email (the only name-like field stored today).</param>
public sealed record UserListFilter(string? Search = null, UserStatus? Status = null);

/// <summary>A row in the admin user list.</summary>
/// <param name="DisplayName">
/// Always null for now: the user record stores no display name yet (only the
/// email from the GCIP token). Reserved so clients can render it once it is.
/// </param>
public sealed record UserSummaryDto(int Id, string Email, string? DisplayName, string Status, DateTimeOffset CreatedAt)
{
    public static UserSummaryDto From(User user) =>
        new(user.Id, user.Email, DisplayName: null, user.Status.ToString(), user.CreatedAt);
}

/// <summary>The admin view of one user: the summary fields plus every role they hold.</summary>
/// <param name="DisplayName">Always null for now; see <see cref="UserSummaryDto"/>.</param>
/// <param name="Roles">Active role assignments in every scope.</param>
public sealed record UserDetailDto(
    int Id,
    string Email,
    string? DisplayName,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AssignedRoleDto> Roles)
{
    public static UserDetailDto From(User user, IReadOnlyList<AssignedRoleDto> roles) =>
        new(user.Id, user.Email, DisplayName: null, user.Status.ToString(), user.CreatedAt, roles);
}

/// <param name="ActorUid">The caller's UID from the validated token's <c>sub</c> claim.</param>
/// <param name="UserId">The user receiving the role, from the route.</param>
/// <param name="RoleId">From the request body. Required.</param>
/// <param name="OrganizationId">
/// From the request body. Null assigns the role at platform scope. It is
/// never trusted: the handler re-checks the caller's rights in that scope.
/// </param>
public sealed record AssignRoleCommand(string ActorUid, int UserId, int? RoleId, int? OrganizationId)
{
    /// <summary>The InvalidRequest check a handler runs before anything else: a positive role id and, if given, a positive organization id.</summary>
    public bool IsWellFormed => RoleId is > 0 && OrganizationId is null or > 0;
}
