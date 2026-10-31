namespace ScrapGo.Core.Modules.Identity.Application.Invitations;

/// <param name="AcceptLink">External: carries the one-time token, so treat it as a secret. Internal: the sign-in page.</param>
/// <param name="SignInWithGoogle">An internal invitation: the invitee signs in with their Google Workspace account.</param>
public sealed record InvitationMessage(int InvitationId, string Email, string AcceptLink, DateTimeOffset ExpiresAt, bool SignInWithGoogle = false);

/// <summary>
/// Delivers an invitation to the invitee. V1 only logs (the admin copies the
/// link from the API response); real email is a later task.
/// </summary>
public interface IInvitationSender
{
    Task SendAsync(InvitationMessage message, CancellationToken cancellationToken);
}
