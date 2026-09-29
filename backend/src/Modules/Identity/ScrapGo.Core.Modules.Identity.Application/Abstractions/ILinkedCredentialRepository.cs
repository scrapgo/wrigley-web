namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

public interface ILinkedCredentialRepository
{
    /// <summary>Returns a tracked entity, so changes to it are persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    Task<LinkedCredential?> FindAsync(int userId, string providerName, CancellationToken cancellationToken);

    void Add(LinkedCredential linkedCredential);
}
