namespace ScrapGo.Core.Modules.Identity.Domain.Users;

/// <summary>
/// An application user, keyed separately from the Google Cloud Identity
/// Platform UID. <see cref="IdentityPlatformUid"/> is unique and indexed but
/// never the primary key, and identity is always resolved by UID, never by
/// <see cref="Email"/>. Email is a display attribute, not an authorization key.
/// </summary>
/// <remarks>
/// Other modules (e.g. Organizations) refer to a user by <see cref="Id"/>
/// only; this entity deliberately has no navigation into them.
/// </remarks>
public class User
{
    // For EF Core materialization.
    private User()
    {
    }

    public int Id { get; private set; }

    /// <summary>The Google Cloud Identity Platform UID. Unique, never the primary key.</summary>
    public string IdentityPlatformUid { get; private set; } = string.Empty;

    /// <summary>Display attribute only; never used for identity resolution or authorization.</summary>
    public string Email { get; private set; } = string.Empty;

    public UserStatus Status { get; private set; } = UserStatus.Active;

    /// <summary>
    /// Decided exactly once, at provisioning time, and never re-evaluated on
    /// later requests. See <see cref="UserClassification"/>.
    /// </summary>
    public UserClassification Classification { get; private set; } = UserClassification.External;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Provision(string identityPlatformUid, string email, UserClassification classification, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityPlatformUid);

        return new User
        {
            IdentityPlatformUid = identityPlatformUid,
            Email = email,
            Status = UserStatus.Active,
            Classification = classification,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public bool IsActive => Status == UserStatus.Active;

    public void Disable(DateTimeOffset now)
    {
        Status = UserStatus.Disabled;
        UpdatedAt = now;
    }

    public void Enable(DateTimeOffset now)
    {
        Status = UserStatus.Active;
        UpdatedAt = now;
    }
}
