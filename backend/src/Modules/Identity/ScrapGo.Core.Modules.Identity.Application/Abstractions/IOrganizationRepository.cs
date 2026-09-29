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
}
