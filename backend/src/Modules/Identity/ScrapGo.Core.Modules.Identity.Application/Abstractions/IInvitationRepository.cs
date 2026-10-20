using ScrapGo.Core.Modules.Identity.Application.Invitations;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

public interface IInvitationRepository
{
    void Add(Invitation invitation);

    /// <summary>A tracked invitation, or null.</summary>
    Task<Invitation?> FindAsync(int invitationId, CancellationToken cancellationToken);

    /// <summary>A tracked invitation by token hash, or null.</summary>
    Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Row-locks the invitation with this token hash until the ambient
    /// transaction ends, so two acceptances of one token are serialized. Must
    /// run inside <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>.
    /// </summary>
    Task LockByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the email's lapsed Pending invitations Expired, immediately, so
    /// a new one can take the one-pending-per-email slot.
    /// </summary>
    Task ExpireLapsedAsync(string email, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Invitations by id, newest first, with their effective status at <paramref name="now"/>.</summary>
    Task<PagedResult<InvitationDto>> ListAsync(InvitationStatus? status, PageRequest page, DateTimeOffset now, CancellationToken cancellationToken);
}
