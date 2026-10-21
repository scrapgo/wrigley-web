namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>One role a user holds.</summary>
public sealed record UserRoleDto(int Id, string Key, string Name);

/// <summary>Filters for the admin user list. Every filter is optional; omitted means "any".</summary>
/// <param name="Search">Case-insensitive substring of the email or display name.</param>
public sealed record UserListFilter(string? Search = null, UserStatus? Status = null, UserType? UserType = null);

/// <summary>A row in the admin user list.</summary>
public sealed record UserSummaryDto(
    int Id,
    string Email,
    string? DisplayName,
    string UserType,
    string Status,
    DateTimeOffset? LastSignInAt,
    DateTimeOffset CreatedAt);

/// <summary>The admin view of one user: who they are, their roles and (External users) their supplier links.</summary>
/// <param name="SupplierRecordIds">Linked Quickbase supplier Record IDs, ascending; always empty for Internal users.</param>
public sealed record UserDetailDto(
    int Id,
    string IdentityPlatformUid,
    string Email,
    string? DisplayName,
    string UserType,
    string Status,
    string? SignInProvider,
    DateTimeOffset? LastSignInAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<UserRoleDto> Roles,
    IReadOnlyList<int> SupplierRecordIds)
{
    public static UserDetailDto From(User user, IReadOnlyList<UserRoleDto> roles, IReadOnlyList<int> supplierRecordIds) =>
        new(
            user.Id,
            user.IdentityPlatformUid,
            user.Email,
            user.DisplayName,
            user.UserType.ToString(),
            user.Status.ToString(),
            user.SignInProvider,
            user.LastSignInAt,
            user.CreatedAt,
            roles,
            supplierRecordIds);
}
