namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>One role a user holds.</summary>
public sealed record AssignedRoleDto(int RoleId, string Name);

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
/// <param name="Roles">Active role assignments.</param>
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
public sealed record AssignRoleCommand(string ActorUid, int UserId, int? RoleId)
{
    /// <summary>The InvalidRequest check a handler runs before anything else: a positive role id.</summary>
    public bool IsWellFormed => RoleId is > 0;
}
