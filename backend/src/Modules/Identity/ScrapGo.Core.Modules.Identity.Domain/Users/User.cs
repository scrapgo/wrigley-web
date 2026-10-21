namespace ScrapGo.Core.Modules.Identity.Domain.Users;

/// <summary>
/// An application user, keyed separately from the Google Cloud Identity
/// Platform UID. <see cref="IdentityPlatformUid"/> is unique and indexed but
/// never the primary key, and identity is always resolved by UID, never by
/// <see cref="Email"/>. Email is a display attribute, not an authorization key.
/// </summary>
/// <remarks>
/// Other modules refer to a user by <see cref="Id"/>
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

    /// <summary>The name from the identity provider, when it sent one.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>
    /// Set once, at creation, and never changed: <c>(id, user_type)</c> is the
    /// target of the composite foreign keys that keep roles and supplier links
    /// to users of the right type.
    /// </summary>
    public UserType UserType { get; private set; } = UserType.External;

    public UserStatus Status { get; private set; } = UserStatus.Active;

    /// <summary>The token's <c>firebase.sign_in_provider</c> at the last sign-in, e.g. <c>google.com</c> or <c>password</c>.</summary>
    public string? SignInProvider { get; private set; }

    public DateTimeOffset? LastSignInAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Provision(
        string identityPlatformUid,
        string email,
        UserType userType,
        DateTimeOffset now,
        string? displayName = null,
        string? signInProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityPlatformUid);

        return new User
        {
            IdentityPlatformUid = identityPlatformUid,
            Email = email,
            DisplayName = displayName,
            UserType = userType,
            Status = UserStatus.Active,
            SignInProvider = signInProvider,
            LastSignInAt = signInProvider is null ? null : now,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public bool IsActive => Status == UserStatus.Active;

    /// <param name="displayName">
    /// The token's <c>name</c>, when it carries one: kept in step with the GCIP
    /// profile. A sign-in without a name leaves the stored one alone.
    /// </param>
    public void RecordSignIn(string? signInProvider, DateTimeOffset now, string? displayName = null)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }

        SignInProvider = signInProvider;
        LastSignInAt = now;
        UpdatedAt = now;
    }

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
