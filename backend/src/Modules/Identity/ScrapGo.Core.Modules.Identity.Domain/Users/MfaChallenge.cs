namespace ScrapGo.Core.Modules.Identity.Domain.Users;

/// <summary>
/// An emailed sign-in code for one GCIP sign-in session, identified by the
/// user and the token's <c>auth_time</c> (unchanged across token refreshes, new
/// on every sign-in). Verified once, it covers that whole sign-in. Only a
/// salted SHA-256 of the code is stored.
/// </summary>
public class MfaChallenge
{
    private MfaChallenge()
    {
    }

    public int Id { get; private set; }

    public int UserId { get; private set; }

    /// <summary>The sign-in this challenge belongs to: the token's <c>auth_time</c>.</summary>
    public DateTimeOffset AuthTime { get; private set; }

    /// <summary>Random per code, so equal codes don't hash alike.</summary>
    public string CodeSalt { get; private set; } = string.Empty;

    /// <summary>Hex SHA-256 of the salt and the code.</summary>
    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Wrong guesses at the current code.</summary>
    public int Attempts { get; private set; }

    /// <summary>Codes sent for this sign-in.</summary>
    public int SendCount { get; private set; }

    public DateTimeOffset LastSentAt { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsVerified => VerifiedAt is not null;

    public static MfaChallenge Create(
        int userId, DateTimeOffset authTime, string codeSalt, string codeHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeSalt);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);

        return new MfaChallenge
        {
            UserId = userId,
            AuthTime = authTime,
            CodeSalt = codeSalt,
            CodeHash = codeHash,
            ExpiresAt = expiresAt,
            SendCount = 1,
            LastSentAt = now,
            CreatedAt = now,
        };
    }

    /// <summary>A new code replaces the old one, with a fresh expiry and attempt count.</summary>
    public void Reissue(string codeSalt, string codeHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeSalt);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        CodeSalt = codeSalt;
        CodeHash = codeHash;
        ExpiresAt = expiresAt;
        Attempts = 0;
        SendCount++;
        LastSentAt = now;
    }

    public void RecordFailedAttempt() => Attempts++;

    public void MarkVerified(DateTimeOffset now) => VerifiedAt = now;
}
