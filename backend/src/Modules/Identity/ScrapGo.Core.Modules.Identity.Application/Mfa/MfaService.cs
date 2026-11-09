using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Mfa;

public enum MfaOutcome
{
    Success,

    /// <summary>MFA is off, or the caller is not an External user: there is nothing to verify.</summary>
    NotRequired,

    /// <summary>This sign-in is already verified.</summary>
    AlreadyVerified,

    /// <summary>The token has no <c>auth_time</c>, so there is no sign-in to attach the code to.</summary>
    InvalidSession,

    /// <summary>A code was sent less than <see cref="MfaService.ResendCooldown"/> ago.</summary>
    TooSoon,

    /// <summary>All <see cref="MfaService.MaxSends"/> codes for this sign-in were sent; sign in again.</summary>
    SendLimitReached,

    /// <summary>Email delivery isn't configured or failed.</summary>
    EmailUnavailable,

    /// <summary>No code has been sent for this sign-in.</summary>
    NoChallenge,

    /// <summary>The code is wrong.</summary>
    InvalidCode,

    /// <summary>The code is past its expiry; send a new one.</summary>
    CodeExpired,

    /// <summary>Too many wrong codes: send a new one.</summary>
    Locked,
}

/// <param name="EmailHint">Where the code went, masked: <c>j***@acme.com</c>.</param>
/// <param name="ResendAvailableAt">When "Resend code" may be used.</param>
public sealed record MfaCodeSentDto(string EmailHint, DateTimeOffset ExpiresAt, DateTimeOffset ResendAvailableAt);

public sealed record MfaResult(MfaOutcome Outcome, MfaCodeSentDto? Sent = null, DateTimeOffset? RetryAt = null);

/// <summary>
/// Email MFA for External users (when <see cref="ExternalUserMfaOptions.RequireForExternalUsers"/>
/// is on): a 6-digit code emailed for each sign-in, identified by the token's
/// <c>auth_time</c>. Internal users sign in with Google Workspace, which has
/// its own 2-step verification, and are never asked.
/// </summary>
public sealed class MfaService(
    ICurrentAccess currentAccess,
    IUserRepository users,
    IMfaChallengeRepository challenges,
    IMfaCodeSender sender,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    IOptions<ExternalUserMfaOptions> mfaOptions,
    TimeProvider timeProvider,
    ILogger<MfaService> logger)
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    /// <summary>Codes per sign-in.</summary>
    public const int MaxSends = 5;

    /// <summary>Wrong guesses per code before it is locked.</summary>
    public const int MaxAttempts = 5;

    /// <summary>Whether this caller's sign-in still needs a code. Used by the per-request gate.</summary>
    public async Task<bool> IsRequiredAsync(AccessContext access, DateTimeOffset? authTime, CancellationToken cancellationToken) =>
        mfaOptions.Value.RequireForExternalUsers
        && access.UserType == UserType.External
        && (authTime is not { } signIn || !await challenges.IsVerifiedAsync(access.UserId, signIn, cancellationToken));

    /// <summary>Emails a new code for this sign-in, replacing any earlier one.</summary>
    public async Task<MfaResult> SendAsync(DateTimeOffset? authTime, CancellationToken cancellationToken)
    {
        if (await RequiringAccessAsync(cancellationToken) is not { } access)
        {
            return new(MfaOutcome.NotRequired);
        }

        if (authTime is not { } signIn)
        {
            return new(MfaOutcome.InvalidSession);
        }

        var now = timeProvider.GetUtcNow();
        var challenge = await challenges.FindAsync(access.UserId, signIn, cancellationToken);
        if (challenge is { IsVerified: true })
        {
            return new(MfaOutcome.AlreadyVerified);
        }

        if (challenge is not null)
        {
            if (challenge.SendCount >= MaxSends)
            {
                return new(MfaOutcome.SendLimitReached);
            }

            if (now - challenge.LastSentAt < ResendCooldown)
            {
                return new(MfaOutcome.TooSoon, RetryAt: challenge.LastSentAt + ResendCooldown);
            }
        }

        if (await users.GetByIdAsync(access.UserId, cancellationToken) is not { } user)
        {
            return new(MfaOutcome.NotRequired);
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var salt = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var expiresAt = now + CodeLifetime;
        if (challenge is null)
        {
            challenge = MfaChallenge.Create(access.UserId, signIn, salt, Hash(salt, code), expiresAt, now);
            challenges.Add(challenge);
        }
        else
        {
            challenge.Reissue(salt, Hash(salt, code), expiresAt, now);
        }

        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.MfaCodeSent,
            UserId: access.UserId,
            Metadata: JsonSerializer.Serialize(new { sendCount = challenge.SendCount })));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException e) when (e.ConstraintName == IdentityUniqueConstraints.MfaChallengeSignIn)
        {
            // Another tab sent one at the same moment: that code stands.
            return new(MfaOutcome.TooSoon, RetryAt: now + ResendCooldown);
        }

        bool sent;
        try
        {
            sent = await sender.SendAsync(new MfaCodeMessage(user.Email, code, expiresAt), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The sign-in code for user {UserId} could not be emailed.", access.UserId);
            sent = false;
        }

        return sent
            ? new(MfaOutcome.Success, new MfaCodeSentDto(MaskEmail(user.Email), expiresAt, now + ResendCooldown))
            : new(MfaOutcome.EmailUnavailable);
    }

    /// <summary>Checks the code; a right one verifies this sign-in.</summary>
    public async Task<MfaOutcome> VerifyAsync(DateTimeOffset? authTime, string? code, CancellationToken cancellationToken)
    {
        if (await RequiringAccessAsync(cancellationToken) is not { } access)
        {
            return MfaOutcome.NotRequired;
        }

        if (authTime is not { } signIn)
        {
            return MfaOutcome.InvalidSession;
        }

        if (await challenges.FindAsync(access.UserId, signIn, cancellationToken) is not { } challenge)
        {
            return MfaOutcome.NoChallenge;
        }

        if (challenge.IsVerified)
        {
            return MfaOutcome.AlreadyVerified;
        }

        if (challenge.Attempts >= MaxAttempts)
        {
            return MfaOutcome.Locked;
        }

        var now = timeProvider.GetUtcNow();
        if (challenge.ExpiresAt <= now)
        {
            return MfaOutcome.CodeExpired;
        }

        var normalized = new string((code ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        var matches = normalized.Length == 6 && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Hash(challenge.CodeSalt, normalized)), Encoding.ASCII.GetBytes(challenge.CodeHash));

        if (!matches)
        {
            challenge.RecordFailedAttempt();
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.MfaFailed,
                UserId: access.UserId,
                Metadata: JsonSerializer.Serialize(new { attempts = challenge.Attempts })));
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return challenge.Attempts >= MaxAttempts ? MfaOutcome.Locked : MfaOutcome.InvalidCode;
        }

        challenge.MarkVerified(now);
        auditLog.Record(new AuditEvent(IdentityAuditEventTypes.MfaVerified, UserId: access.UserId));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MfaOutcome.Success;
    }

    /// <summary>The caller's access context, when this sign-in is subject to email MFA at all.</summary>
    private async Task<AccessContext?> RequiringAccessAsync(CancellationToken cancellationToken) =>
        mfaOptions.Value.RequireForExternalUsers
        && await currentAccess.GetAsync(cancellationToken) is { UserType: UserType.External, IsActive: true } access
            ? access
            : null;

    private static string Hash(string salt, string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{code}")));

    private static string MaskEmail(string email)
    {
        var at = email.LastIndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }
}
