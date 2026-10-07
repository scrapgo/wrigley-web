using ScrapGo.Core.Modules.Identity.Application.Applications;

namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>The wire shape of the caller's own user record. Entities never leave this layer.</summary>
/// <param name="Roles">Every active role the caller holds, platform-scoped and per organization.</param>
/// <param name="Permissions">
/// The caller's effective permission names, one entry per scope they hold
/// anything in. Permissions are never flattened across scopes: holding
/// <c>Role.Update</c> in one organization grants nothing at platform scope or
/// in another organization, and a client gate must check the right scope.
/// Application scopes carry both <c>organizationId</c> and <c>applicationId</c>.
/// </param>
/// <param name="Organizations">
/// The same access as a tree for UI gating: each active organization →
/// applications the caller holds something in → the enabled modules they hold
/// something in → permissions.
/// </param>
/// <param name="WorkspaceSignInRequired">
/// True when the caller holds a platform role that this sign-in can't use
/// because it isn't a Google Workspace sign-in (Decision 12). The UI should
/// prompt "Sign in with Google". Grants nothing.
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
    IReadOnlyList<ScopedPermissionsDto> Permissions,
    IReadOnlyList<OrganizationAccessDto> Organizations,
    bool WorkspaceSignInRequired = false)
{
    public static CurrentUserDto From(User user) =>
        new(user.Id, user.IdentityPlatformUid, user.Email, user.Status.ToString(), user.Classification.ToString(), [], [], []);
}
