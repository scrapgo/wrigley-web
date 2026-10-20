namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>The wire shape of the caller's own user record. Entities never leave this layer.</summary>
/// <param name="Roles">Every unexpired role the caller holds.</param>
/// <param name="Permissions">The caller's effective permission names, ordered by name.</param>
/// <param name="WorkspaceSignInRequired">
/// True when the caller holds a role that this sign-in can't use because it
/// isn't a Google Workspace sign-in (Decision 12). The UI should prompt
/// "Sign in with Google". Grants nothing.
/// </param>
/// <remarks>
/// <see cref="From"/> leaves <see cref="Roles"/> and <see cref="Permissions"/>
/// empty; <see cref="GetCurrentUserHandler"/> fills them from
/// <c>PermissionResolver.GetEffectiveAccessAsync</c>.
/// </remarks>
public sealed record CurrentUserDto(
    int Id,
    string IdentityPlatformUid,
    string Email,
    string Status,
    string Classification,
    IReadOnlyList<AssignedRoleDto> Roles,
    IReadOnlyList<string> Permissions,
    bool WorkspaceSignInRequired = false)
{
    public static CurrentUserDto From(User user) =>
        new(user.Id, user.IdentityPlatformUid, user.Email, user.Status.ToString(), user.Classification.ToString(), [], []);
}
