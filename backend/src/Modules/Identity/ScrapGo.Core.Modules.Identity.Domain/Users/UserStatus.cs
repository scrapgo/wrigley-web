namespace ScrapGo.Core.Modules.Identity.Domain.Users;

/// <summary>
/// Stored as <c>text</c> plus a named CHECK constraint (<c>ck_users_status</c>),
/// not a native Postgres enum.
/// </summary>
public enum UserStatus
{
    /// <summary>The user may authenticate and act through the application.</summary>
    Active,

    /// <summary>
    /// Access revoked. The user can still authenticate with the identity
    /// provider, but the application denies every request.
    /// </summary>
    Disabled,
}
