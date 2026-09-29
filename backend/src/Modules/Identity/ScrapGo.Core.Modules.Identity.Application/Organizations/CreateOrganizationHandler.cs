using System.Text.Json;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Organizations;

/// <param name="IdentityPlatformUid">From the validated token's <c>sub</c> claim.</param>
/// <param name="Email">From the <c>email</c> claim; used only if this request also provisions the caller.</param>
/// <param name="HostedDomain">From the <c>hd</c> claim; used only if this request also provisions the caller.</param>
public sealed record CreateOrganizationCommand(string IdentityPlatformUid, string Email, string? HostedDomain, string? Name);

public enum CreateOrganizationOutcome
{
    Created,

    /// <summary>The name is blank, or has no letter or digit to build a slug from.</summary>
    InvalidName,

    /// <summary>Another organization already has this slug.</summary>
    DuplicateSlug,
}

public sealed record CreateOrganizationResult(CreateOrganizationOutcome Outcome, int? OrganizationId = null);

/// <summary>
/// Creates an organization and makes the caller its first member and
/// <see cref="DefaultRoleNames.OrganizationAdministrator"/>, all in one
/// transaction. The caller is provisioned first if this is their first request.
/// </summary>
public sealed class CreateOrganizationHandler(
    ProvisionCurrentUserHandler provisionCurrentUser,
    IOrganizationRepository organizations,
    IRoleRepository roles,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public async Task<CreateOrganizationResult> HandleAsync(CreateOrganizationCommand command, CancellationToken cancellationToken)
    {
        if (Organization.ToSlug(command.Name).Length == 0)
        {
            return new(CreateOrganizationOutcome.InvalidName);
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // Joins this transaction, so a duplicate slug also rolls back
                // a first-sighting provisioning (and its audit row).
                var creator = await provisionCurrentUser.HandleAsync(
                    new ProvisionCurrentUserCommand(command.IdentityPlatformUid, command.Email, command.HostedDomain), ct);

                var organizationAdministratorRoleId = await roles.GetPlatformRoleIdAsync(DefaultRoleNames.OrganizationAdministrator, ct);
                var now = timeProvider.GetUtcNow();

                var organization = Organization.Create(command.Name!, now);
                organizations.Add(organization);

                // Saved on its own first: this is where the database enforces
                // slug uniqueness, and it assigns the id the rows below need.
                await unitOfWork.SaveChangesAsync(ct);

                organizations.AddMembership(OrganizationMembership.Create(creator.Id, organization.Id, now));
                roles.AddUserRole(UserRole.Assign(creator.Id, organizationAdministratorRoleId, organization.Id, now));
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.OrganizationCreated,
                    UserId: creator.Id,
                    OrganizationId: organization.Id,
                    Metadata: JsonSerializer.Serialize(new { name = organization.Name, slug = organization.Slug })));

                await unitOfWork.SaveChangesAsync(ct);

                return new CreateOrganizationResult(CreateOrganizationOutcome.Created, organization.Id);
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.OrganizationSlug)
        {
            return new(CreateOrganizationOutcome.DuplicateSlug);
        }
    }
}
