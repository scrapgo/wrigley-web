namespace ScrapGo.Core.Modules.Identity.Application.Roles;

/// <param name="ActorUid">The caller's UID from the validated token's <c>sub</c> claim.</param>
/// <param name="OrganizationId">
/// The organization the role belongs to. It comes from the request body, so
/// it is never trusted: the caller's membership and <c>Role.Create</c>
/// permission in it are re-checked before anything is written.
/// </param>
public sealed record CreateRoleCommand(string ActorUid, int? OrganizationId, string? Name, string? Description);

public sealed record UpdateRoleCommand(string ActorUid, int RoleId, string? Name, string? Description);

public sealed record DeleteRoleCommand(string ActorUid, int RoleId);

public sealed record RolePermissionCommand(string ActorUid, int RoleId, string? PermissionName);

/// <param name="OrganizationId">The owning organization, or null for a built-in platform-defined role.</param>
public sealed record RoleDto(int Id, string Name, string Description, int? OrganizationId)
{
    public static RoleDto From(Role role) => new(role.Id, role.Name, role.Description, role.OrganizationId);
}

/// <summary>A role plus the catalog permissions it grants, for role read-back.</summary>
/// <param name="OrganizationId">The owning organization, or null for a built-in platform-defined role.</param>
/// <param name="Permissions">Permission names, ordered by name.</param>
public sealed record RoleDetailDto(int Id, string Name, string Description, int? OrganizationId, IReadOnlyList<string> Permissions)
{
    public static RoleDetailDto From(Role role, IReadOnlyList<string> permissions) =>
        new(role.Id, role.Name, role.Description, role.OrganizationId, permissions);
}

public enum RoleMutationOutcome
{
    Success,

    /// <summary>Missing organization id, or a blank name.</summary>
    InvalidRequest,

    /// <summary>
    /// The caller lacks the required permission (<c>Role.Create</c> / <c>Role.Update</c>)
    /// in the role's organization, or has no active membership there.
    /// </summary>
    Forbidden,

    /// <summary>No active, organization-scoped role with that id.</summary>
    NotFound,

    /// <summary>The organization already has a role with this name.</summary>
    DuplicateName,

    /// <summary>Escalation guard: the caller holds this role themselves.</summary>
    CannotModifyOwnRole,
}

public enum RoleReadOutcome
{
    Success,

    /// <summary>
    /// No such role visible from this organization: unknown, soft-deleted, or
    /// another organization's. All three look the same, so callers can't probe
    /// for other organizations' role ids.
    /// </summary>
    NotFound,
}

public sealed record RoleDetailResult(RoleReadOutcome Outcome, RoleDetailDto? Role = null);

public sealed record RolePermissionsResult(RoleReadOutcome Outcome, IReadOnlyList<string>? Permissions = null);

public sealed record RoleMutationResult(RoleMutationOutcome Outcome, RoleDto? Role = null);

public enum RoleDeletionOutcome
{
    Success,

    /// <summary>The caller lacks <c>Role.Delete</c> in the role's organization, or has no active membership there.</summary>
    Forbidden,

    NotFound,

    /// <summary>At least one user role still references it; revoke every assignment first.</summary>
    StillAssigned,

    /// <summary>Escalation guard: the caller holds this role themselves.</summary>
    CannotModifyOwnRole,
}

public enum RolePermissionOutcome
{
    Success,

    /// <summary>The caller lacks <c>Role.Update</c> in the role's organization, or has no active membership there.</summary>
    Forbidden,

    RoleNotFound,

    /// <summary>The name isn't in the seeded permission catalog.</summary>
    UnknownPermission,

    /// <summary>Escalation guard: a caller may only attach permissions they already hold in that organization.</summary>
    CannotGrantUnheldPermission,

    /// <summary>Escalation guard: the caller holds this role themselves.</summary>
    CannotModifyOwnRole,
}
