using Microsoft.Extensions.Logging;
using ScrapGo.Core.Modules.Identity.Application.Mfa;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Mfa;

/// <summary>
/// Used when Gmail isn't configured: nothing can be sent, so the caller gets
/// 503 <c>mfa_email_unavailable</c>. The code is deliberately not logged:
/// anyone with log access could otherwise sign in as the user.
/// </summary>
public sealed partial class LoggingMfaCodeSender(ILogger<LoggingMfaCodeSender> logger) : IMfaCodeSender
{
    public Task<bool> SendAsync(MfaCodeMessage message, CancellationToken cancellationToken)
    {
        LogNotSent(message.Email);

        return Task.FromResult(false);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "A sign-in code for {Email} could not be sent: email delivery isn't configured (Invitations:Gmail).")]
    private partial void LogNotSent(string email);
}
