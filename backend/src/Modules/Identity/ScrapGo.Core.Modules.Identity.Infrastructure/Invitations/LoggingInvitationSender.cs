using Microsoft.Extensions.Logging;
using ScrapGo.Core.Modules.Identity.Application.Invitations;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Invitations;

/// <summary>
/// V1 <see cref="IInvitationSender"/>: records that an invitation is ready.
/// The admin copies the accept link from the create/resend response; real
/// email delivery is a later task.
/// </summary>
/// <remarks>
/// The link carries the one-time token, so it is deliberately not logged:
/// anyone with log access could otherwise accept the invitation.
/// </remarks>
public sealed partial class LoggingInvitationSender(ILogger<LoggingInvitationSender> logger) : IInvitationSender
{
    public Task SendAsync(InvitationMessage message, CancellationToken cancellationToken)
    {
        LogInvitationReady(message.InvitationId, message.Email, message.ExpiresAt);

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Invitation {InvitationId} for {Email} is ready (expires {ExpiresAt:o}). Email delivery isn't configured; the admin shares the accept link.")]
    private partial void LogInvitationReady(int invitationId, string email, DateTimeOffset expiresAt);
}
