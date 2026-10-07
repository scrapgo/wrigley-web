namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

public interface IInvitationRepository
{
    void Add(Invitation invitation);

    /// <summary>A tracked invitation of exactly this organization, with its grants, or null.</summary>
    Task<Invitation?> FindAsync(int organizationId, int invitationId, CancellationToken cancellationToken);

    /// <summary>A tracked invitation by its token hash, with its grants, or null.</summary>
    Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>The tracked pending invitation for this email in this organization (expired or not), or null.</summary>
    Task<Invitation?> FindPendingAsync(int organizationId, string emailNormalized, CancellationToken cancellationToken);

    /// <summary>Pending invitations of this organization, with grants, untracked, newest first.</summary>
    Task<IReadOnlyList<Invitation>> ListPendingAsync(int organizationId, CancellationToken cancellationToken);
}
