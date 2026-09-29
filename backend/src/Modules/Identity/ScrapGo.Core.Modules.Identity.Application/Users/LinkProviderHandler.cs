using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <param name="IdentityPlatformUid">The primary bearer token's already-validated <c>sub</c> claim.</param>
/// <param name="ReauthIdToken">A second, independent GCIP ID token from the client's just-completed re-authentication.</param>
public sealed record LinkProviderCommand(string IdentityPlatformUid, string ReauthIdToken);

public enum LinkProviderOutcome
{
    /// <summary>Linked and audit-logged.</summary>
    Linked,

    /// <summary>The re-auth token failed signature, issuer, audience or lifetime validation.</summary>
    InvalidReauthToken,

    /// <summary>The re-auth token's <c>sub</c> is not the caller's UID.</summary>
    ReauthIdentityMismatch,

    /// <summary>The re-auth token is valid for the caller, but its <c>auth_time</c> is outside the freshness window.</summary>
    ReauthNotFresh,

    /// <summary>The caller has no user row yet.</summary>
    UserNotProvisioned,
}

/// <param name="Provider">The linked provider id (e.g. <c>"google.com"</c>), when <see cref="Outcome"/> is <see cref="LinkProviderOutcome.Linked"/>.</param>
public sealed record LinkProviderResult(LinkProviderOutcome Outcome, string? Provider = null);

/// <summary>
/// Self-service account linking: links an extra GCIP sign-in provider to the
/// caller, but only after a second, independently validated token proves the
/// caller just re-authenticated as that same identity.
/// </summary>
/// <remarks>
/// Never auto-link by email match: identity is compared by UID only, and email
/// is never read in this decision. The checks run in a fixed order (token
/// validity, identity match, freshness, provisioning), from the cheapest to
/// explain to a caller to the most expensive.
/// </remarks>
public sealed class LinkProviderHandler(
    IReauthTokenValidator reauthTokenValidator,
    IUserRepository users,
    ILinkedCredentialRepository linkedCredentials,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    /// <summary>
    /// How recent <c>auth_time</c> must be for the token to prove the re-auth
    /// flow just ran, rather than being a replayed, still-unexpired token.
    /// </summary>
    public static readonly TimeSpan ReauthFreshnessWindow = TimeSpan.FromMinutes(5);

    public async Task<LinkProviderResult> HandleAsync(LinkProviderCommand command, CancellationToken cancellationToken)
    {
        var reauth = await reauthTokenValidator.ValidateAsync(command.ReauthIdToken, cancellationToken);
        if (!reauth.IsValid)
        {
            return new(LinkProviderOutcome.InvalidReauthToken);
        }

        if (!string.Equals(reauth.Uid, command.IdentityPlatformUid, StringComparison.Ordinal))
        {
            return new(LinkProviderOutcome.ReauthIdentityMismatch);
        }

        var now = timeProvider.GetUtcNow();
        if (reauth.AuthTime is not { } authTime || now - authTime > ReauthFreshnessWindow)
        {
            return new(LinkProviderOutcome.ReauthNotFresh);
        }

        if (await users.GetIdByUidAsync(command.IdentityPlatformUid, cancellationToken) is not { } userId)
        {
            return new(LinkProviderOutcome.UserNotProvisioned);
        }

        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.ProviderLinked,
            UserId: userId,
            Metadata: JsonSerializer.Serialize(new { provider = reauth.SignInProvider })));

        // Upserted, not appended: the table mirrors current linked-provider state.
        if (!string.IsNullOrWhiteSpace(reauth.SignInProvider))
        {
            var existing = await linkedCredentials.FindAsync(userId, reauth.SignInProvider, cancellationToken);
            if (existing is null)
            {
                linkedCredentials.Add(LinkedCredential.Link(userId, reauth.SignInProvider, now));
            }
            else
            {
                existing.Relink(now);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new(LinkProviderOutcome.Linked, reauth.SignInProvider);
    }
}
