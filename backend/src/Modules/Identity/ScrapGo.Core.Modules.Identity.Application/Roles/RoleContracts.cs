namespace ScrapGo.Core.Modules.Identity.Application.Roles;

/// <summary>A role and the permission keys it grants.</summary>
/// <param name="Permissions">Granted permission keys, ordered.</param>
/// <param name="HolderCount">How many users hold the role.</param>
public sealed record RoleDto(
    int Id,
    string Key,
    string Name,
    string? Description,
    string UserType,
    bool IsSystem,
    IReadOnlyList<string> Permissions,
    int HolderCount);

/// <summary>One permission in the catalog.</summary>
/// <param name="Audience">Internal, External or Any: which roles it may be mapped to.</param>
public sealed record PermissionDto(int Id, string Key, string Name, string Description, string Audience);

/// <summary>The permissions one feature owns.</summary>
public sealed record PermissionGroupDto(string FeatureKey, string FeatureName, IReadOnlyList<PermissionDto> Permissions);

/// <param name="Permissions">Every permission key the role should grant; keys not listed are removed.</param>
public sealed record SetRolePermissionsCommand(int RoleId, IReadOnlyList<string>? Permissions);

public enum RoleAdminOutcome
{
    /// <summary>Done, or already so (nothing written then).</summary>
    Success,

    /// <summary>A missing list or a blank key.</summary>
    InvalidRequest,

    NotFound,

    /// <summary>A key that isn't in the catalog: admins map keys to roles, they never invent them.</summary>
    UnknownPermission,

    /// <summary>An Internal permission on an external role, or an External one on an internal role.</summary>
    AudienceMismatch,
}

public sealed record RoleAdminResult(RoleAdminOutcome Outcome, RoleDto? Role = null);
