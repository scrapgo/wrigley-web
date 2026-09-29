namespace ScrapGo.Core.Modules.Identity.Application.Roles;

/// <param name="ActorUid">The caller's UID from the validated token's <c>sub</c> claim.</param>
/// <param name="OrganizationId">
/// The organization the role belongs to. It comes from the request body, so
/// it is never trusted: the caller's administrator rights in it are
/// re-checked before anything is written.
/// </param>
public sealed record CreateRoleCommand(string ActorUid, int? OrganizationId, string? Name, string? Description);

public sealed record UpdateRoleCommand(string ActorUid, int RoleId, string? Name, string? Description);

public sealed record DeleteRoleCommand(string ActorUid, int RoleId);

public sealed record RolePermissionCommand(string ActorUid, int RoleId, string? PermissionName);

public sealed record RoleDto(int Id, string Name, string Description)
{
    public static RoleDto From(Role role) => new(role.Id, role.Name, role.Description);
}

public enum RoleMutationOutcome
{
    Success,

    /// <summary>Missing organization id, or a blank name.</summary>
    InvalidRequest,

    /// <summary>The caller is not an OrganizationAdministrator of the role's organization.</summary>
    Forbidden,

    /// <summary>No active, organization-scoped role with that id.</summary>
    NotFound,

    /// <summary>The organization already has a role with this name.</summary>
    DuplicateName,
}

public sealed record RoleMutationResult(RoleMutationOutcome Outcome, RoleDto? Role = null);

public enum RoleDeletionOutcome
{
    Success,
    Forbidden,
    NotFound,

    /// <summary>At least one user role still references it; revoke every assignment first.</summary>
    StillAssigned,
}

public enum RolePermissionOutcome
{
    Success,
    Forbidden,
    RoleNotFound,

    /// <summary>The name isn't in the seeded permission catalog.</summary>
    UnknownPermission,
}
