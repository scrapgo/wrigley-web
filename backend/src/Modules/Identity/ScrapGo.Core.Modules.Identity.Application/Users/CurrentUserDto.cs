namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>The wire shape of the caller's own user record. Entities never leave this layer.</summary>
/// <param name="Roles">Every active role the caller holds, platform-scoped and per organization.</param>
/// <param name="Permissions">
/// The caller's effective permission names, one entry per scope they hold
/// anything in. Permissions are never flattened across scopes: holding
/// <c>Role.Update</c> in one organization grants nothing at platform scope or
/// in another organization, and a client gate must check the right scope.
/// </param>
/// <remarks>
/// <see cref="From"/> leaves <see cref="Roles"/> and <see cref="Permissions"/>
/// empty; <see cref="GetCurrentUserHandler"/> fills them from
/// <c>PermissionResolver.GetEffectiveAccessAsync</c>. Organization scopes
/// appear only where the caller has an active membership.
/// </remarks>
public sealed record CurrentUserDto(
    int Id,
    string IdentityPlatformUid,
    string Email,
    string Status,
    string Classification,
    IReadOnlyList<AssignedRoleDto> Roles,
    IReadOnlyList<ScopedPermissionsDto> Permissions)
{
    public static CurrentUserDto From(User user) =>
        new(user.Id, user.IdentityPlatformUid, user.Email, user.Status.ToString(), user.Classification.ToString(), [], []);
}
