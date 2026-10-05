namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

/// <summary>
/// Thrown by <see cref="IUnitOfWork.SaveChangesAsync"/> when a save violates a
/// unique constraint. Handlers match on <see cref="ConstraintName"/> (one of
/// <see cref="IdentityUniqueConstraints"/>) to turn a lost race into a
/// typed outcome, instead of doing a check-then-insert that could race.
/// </summary>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : Exception($"Unique constraint '{constraintName}' was violated.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}

/// <summary>
/// Names of the unique constraints handlers react to. Part of the schema
/// contract: the EF configurations name their indexes from these constants.
/// </summary>
public static class IdentityUniqueConstraints
{
    public const string OrganizationSlug = "ux_organizations_slug";

    public const string RoleNamePerOrganization = "ux_roles_organization_id_name";

    /// <summary>A user has at most one membership row per organization.</summary>
    public const string OrganizationMembership = "ux_organization_memberships_user_id_organization_id";

    /// <summary>A user holds a given role in a given organization at most once.</summary>
    public const string OrganizationRoleAssignment = "ux_user_roles_user_id_role_id_organization_id";

    /// <summary>A user holds a given role at platform scope at most once.</summary>
    public const string PlatformRoleAssignment = "ux_user_roles_user_id_role_id_platform_scope";
}
