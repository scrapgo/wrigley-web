namespace ScrapGo.Core.Modules.Identity.Application.Invitations;

/// <param name="AcceptLink">External: carries the one-time token, so treat it as a secret. Internal: the sign-in page.</param>
/// <param name="SignInWithGoogle">An internal invitation: the invitee signs in with their Google Workspace account.</param>
public sealed record InvitationMessage(int InvitationId, string Email, string AcceptLink, DateTimeOffset ExpiresAt, bool SignInWithGoogle = false);

/// <summary>
/// Delivers an invitation to the invitee: by Gmail when configured, otherwise
/// only logged (the admin copies the link from the API response).
/// </summary>
public interface IInvitationSender
{
    /// <returns>True when the email went out; false when delivery isn't configured. Throws when sending fails.</returns>
    Task<bool> SendAsync(InvitationMessage message, CancellationToken cancellationToken);
}
