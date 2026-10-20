namespace ScrapGo.Core.Modules.Identity.Application.Invitations;

/// <param name="AcceptLink">Carries the one-time token: treat it as a secret.</param>
public sealed record InvitationMessage(int InvitationId, string Email, string AcceptLink, DateTimeOffset ExpiresAt);

/// <summary>
/// Delivers an invitation to the invitee. V1 only logs (the admin copies the
/// link from the API response); real email is a later task.
/// </summary>
public interface IInvitationSender
{
    Task SendAsync(InvitationMessage message, CancellationToken cancellationToken);
}
