namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

public interface IOrganizationRepository
{
    /// <summary>
    /// Stages a new organization. Slug uniqueness is enforced by the database
    /// on save (<see cref="IdentityUniqueConstraints.OrganizationSlug"/>), so
    /// there is no check-then-insert race.
    /// </summary>
    void Add(Organization organization);

    void AddMembership(OrganizationMembership membership);

    /// <summary>A tracked organization, or null. Mutate it through its domain methods and save through <see cref="IUnitOfWork"/>.</summary>
    Task<Organization?> GetByIdAsync(int organizationId, CancellationToken cancellationToken);

    /// <summary>A tracked membership (any status), or null.</summary>
    Task<OrganizationMembership?> FindMembershipAsync(int userId, int organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Stages a hard delete of a membership found by <see cref="FindMembershipAsync"/>.
    /// It does not touch the user's roles in that organization; the caller
    /// decides whether to revoke them too.
    /// </summary>
    void RemoveMembership(OrganizationMembership membership);
}
