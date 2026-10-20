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
    /// <summary>A user holds a given role at most once: <c>user_roles</c>' primary key.</summary>
    public const string RoleAssignment = "pk_user_roles";

    /// <summary>Invitation tokens are unique.</summary>
    public const string InvitationTokenHash = "ux_invitations_token_hash";

    /// <summary>At most one pending invitation per email.</summary>
    public const string PendingInvitationEmail = "ux_invitations_email_pending";
}
