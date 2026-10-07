using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Organizations;

/// <param name="ActorUid">The platform administrator's UID, from the validated token's <c>sub</c> claim.</param>
/// <param name="Name">The organization's display name; its slug is derived from it.</param>
/// <param name="FirstAdminUserId">
/// The existing user who becomes the organization's first
/// OrganizationAdministrator. Every organization is born with one, so none is
/// ever unmanaged.
/// </param>
/// <param name="FirstAdminEmail">
/// Instead of <paramref name="FirstAdminUserId"/>: someone who hasn't signed in
/// yet. They get an invitation pre-granting OrganizationAdministrator, valid
/// once accepted with that verified email. Exactly one of the two is required.
/// </param>
public sealed record CreateOrganizationCommand(string ActorUid, string? Name, int? FirstAdminUserId, string? FirstAdminEmail = null);

public enum CreateOrganizationOutcome
{
    Created,

    /// <summary>The name is blank, or has no letter or digit to build a slug from.</summary>
    InvalidName,

    /// <summary>Neither or both of a first administrator id and email, a non-positive id, or a malformed email.</summary>
    InvalidFirstAdmin,

    /// <summary>The named first administrator isn't a provisioned user.</summary>
    FirstAdminNotFound,

    /// <summary>Another organization already has this slug.</summary>
    DuplicateSlug,
}

/// <param name="Invitation">Set when the first administrator was invited by email: the one-time token to pass on.</param>
public sealed record CreateOrganizationResult(
    CreateOrganizationOutcome Outcome, int? OrganizationId = null, CreatedInvitationDto? Invitation = null);

/// <summary>
/// Creates an organization for a customer (platform administrators only,
/// ORG-APP-MODULE-MODEL.md Decision 11). The named first administrator gets an
/// active membership and the built-in OrganizationAdministrator role there, all
/// in one transaction. The creating platform administrator gets nothing: they
/// are not a member.
/// </summary>
/// <remarks>
/// The first administrator may be an external user (Decision 4a): customer
/// organizations are administered by the customer.
/// </remarks>
public sealed class CreateOrganizationHandler(
    InvitationService invitations,
    IUserRepository users,
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

        var byEmail = !string.IsNullOrWhiteSpace(command.FirstAdminEmail);
        if (byEmail == command.FirstAdminUserId is not null
            || (!byEmail && command.FirstAdminUserId is not > 0)
            || (byEmail && !command.FirstAdminEmail!.Contains('@', StringComparison.Ordinal)))
        {
            return new(CreateOrganizationOutcome.InvalidFirstAdmin);
        }

        if (!byEmail && await users.GetByIdAsync(command.FirstAdminUserId!.Value, cancellationToken) is null)
        {
            return new(CreateOrganizationOutcome.FirstAdminNotFound);
        }

        var actorUserId = await users.GetIdByUidAsync(command.ActorUid, cancellationToken);

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var organizationAdministratorRoleId = await roles.GetPlatformRoleIdAsync(DefaultRoleNames.OrganizationAdministrator, ct);
                var now = timeProvider.GetUtcNow();

                var organization = Organization.Create(command.Name!, now);
                organizations.Add(organization);

                // Saved on its own first: this is where the database enforces
                // slug uniqueness, and it assigns the id the rows below need.
                await unitOfWork.SaveChangesAsync(ct);

                if (byEmail)
                {
                    auditLog.Record(new AuditEvent(
                        IdentityAuditEventTypes.OrganizationCreated,
                        UserId: actorUserId,
                        OrganizationId: organization.Id,
                        Metadata: JsonSerializer.Serialize(new { name = organization.Name, slug = organization.Slug, firstAdminInvited = true })));
                    await unitOfWork.SaveChangesAsync(ct);

                    // Joins this transaction: no organization without its invitation.
                    var invited = await invitations.CreateAsync(
                        new CreateInvitationCommand(
                            command.ActorUid,
                            ActingAsPlatformAdmin: true,
                            organization.Id,
                            command.FirstAdminEmail,
                            [new InvitationGrantRequest(organizationAdministratorRoleId, ApplicationId: null)]),
                        ct);

                    return invited.Invitation is { } invitation
                        ? new CreateOrganizationResult(CreateOrganizationOutcome.Created, organization.Id, invitation)
                        : throw new InvalidOperationException($"First-admin invitation failed: {invited.Outcome}.");
                }

                var firstAdminId = command.FirstAdminUserId!.Value;
                organizations.AddMembership(OrganizationMembership.Create(firstAdminId, organization.Id, now));
                roles.AddUserRole(UserRole.Assign(firstAdminId, organizationAdministratorRoleId, organization.Id, now));

                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.OrganizationCreated,
                    UserId: actorUserId,
                    OrganizationId: organization.Id,
                    Metadata: JsonSerializer.Serialize(new { name = organization.Name, slug = organization.Slug, firstAdminUserId = firstAdminId })));
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.MembershipAdded,
                    UserId: actorUserId,
                    OrganizationId: organization.Id,
                    Metadata: JsonSerializer.Serialize(new { targetUserId = firstAdminId, reactivated = false })));
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.RoleAssigned,
                    UserId: actorUserId,
                    OrganizationId: organization.Id,
                    Metadata: JsonSerializer.Serialize(new { targetUserId = firstAdminId, roleId = organizationAdministratorRoleId, organizationId = organization.Id })));

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
