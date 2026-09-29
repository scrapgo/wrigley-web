namespace ScrapGo.Core.Modules.Identity.Domain.Users;

/// <summary>
/// Records that a <see cref="User"/> has linked a GCIP sign-in provider. It
/// mirrors current linked-provider state (one row per user and provider), not
/// a history; GCIP remains the source of truth for whether a provider is
/// actually linked.
/// </summary>
public class LinkedCredential
{
    private LinkedCredential()
    {
    }

    public int Id { get; private set; }

    public int UserId { get; private set; }

    /// <summary>
    /// Null for a GCIP-native provider (e.g. a personal Google account). Set
    /// only when the credential is tied to a specific organization's
    /// SAML/OIDC identity provider, which the SSO module owns. That module
    /// hasn't been migrated yet, so this is a plain id with no navigation.
    /// </summary>
    public int? IdentityProviderId { get; private set; }

    /// <summary>GCIP's own sign-in-provider id, verbatim (e.g. <c>"google.com"</c>).</summary>
    public string ProviderName { get; private set; } = string.Empty;

    public LinkedCredentialStatus Status { get; private set; } = LinkedCredentialStatus.Active;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static LinkedCredential Link(int userId, string providerName, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        return new LinkedCredential
        {
            UserId = userId,
            ProviderName = providerName,
            Status = LinkedCredentialStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Re-linking an already-known provider reactivates the existing row rather than adding another.</summary>
    public void Relink(DateTimeOffset now)
    {
        Status = LinkedCredentialStatus.Active;
        UpdatedAt = now;
    }
}
