namespace ScrapGo.Core.Modules.Identity.Application.Mfa;

/// <param name="Code">The 6-digit code. Never logged.</param>
public sealed record MfaCodeMessage(string Email, string Code, DateTimeOffset ExpiresAt);

/// <summary>Delivers a sign-in code by email.</summary>
public interface IMfaCodeSender
{
    /// <returns>True when the email went out; false when email delivery isn't configured.</returns>
    Task<bool> SendAsync(MfaCodeMessage message, CancellationToken cancellationToken);
}
