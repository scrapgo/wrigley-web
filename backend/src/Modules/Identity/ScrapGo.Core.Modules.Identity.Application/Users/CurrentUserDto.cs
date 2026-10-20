namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>The wire shape of the caller's own user record. Entities never leave this layer.</summary>
/// <param name="Roles">Every unexpired role the caller holds.</param>
/// <param name="Permissions">The caller's effective permission keys (granted, and feature on), ordered.</param>
/// <remarks>
/// <see cref="From"/> leaves <see cref="Roles"/> and <see cref="Permissions"/>
/// empty; <see cref="GetCurrentUserHandler"/> fills them from
/// the access context.
/// </remarks>
public sealed record CurrentUserDto(
    int Id,
    string IdentityPlatformUid,
    string Email,
    string Status,
    string UserType,
    IReadOnlyList<AssignedRoleDto> Roles,
    IReadOnlyList<string> Permissions)
{
    public static CurrentUserDto From(User user) =>
        new(user.Id, user.IdentityPlatformUid, user.Email, user.Status.ToString(), user.UserType.ToString(), [], []);
}
