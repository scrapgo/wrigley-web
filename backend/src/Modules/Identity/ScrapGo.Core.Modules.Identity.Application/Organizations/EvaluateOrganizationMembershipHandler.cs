using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Organizations;

public enum OrganizationAccessDecision
{
    Allow,

    /// <summary>No active membership in the organization (or no such organization).</summary>
    Deny,

    /// <summary>
    /// The caller is an active member, but the organization is deactivated.
    /// Only ever reported to members, so it tells nobody else that the
    /// organization exists.
    /// </summary>
    DenyOrganizationDeactivated,
}

/// <summary>
/// The cross-tenant (IDOR) guard: a caller may reach an organization-scoped
/// route only with an active membership in an active organization. The
/// organization id comes from the route, never from the token. Every denial
/// is audit-logged.
/// </summary>
public sealed class EvaluateOrganizationMembershipHandler(
    IUserRepository users,
    IAuthorizationQueries queries,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog)
{
    public async Task<OrganizationAccessDecision> HandleAsync(
        string identityPlatformUid, int organizationId, CancellationToken cancellationToken)
    {
        var userId = await users.GetIdByUidAsync(identityPlatformUid, cancellationToken);

        if (userId is { } id && await queries.HasActiveMembershipAsync(id, organizationId, cancellationToken))
        {
            return OrganizationAccessDecision.Allow;
        }

        var decision = userId is { } memberId
            && await queries.IsMemberOfDeactivatedOrganizationAsync(memberId, organizationId, cancellationToken)
                ? OrganizationAccessDecision.DenyOrganizationDeactivated
                : OrganizationAccessDecision.Deny;

        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.DeniedCrossTenantAccess,
            UserId: userId,
            OrganizationId: organizationId,
            Metadata: decision == OrganizationAccessDecision.DenyOrganizationDeactivated
                ? JsonSerializer.Serialize(new { reason = "organization_deactivated" })
                : null));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return decision;
    }

    /// <summary>
    /// For an application-scoped route, after membership was allowed: true when
    /// the organization has the application (assigned, and active in the
    /// catalog). A denial is audited.
    /// </summary>
    public async Task<bool> HandleApplicationAsync(
        string identityPlatformUid, int organizationId, int applicationId, CancellationToken cancellationToken)
    {
        if (await queries.IsApplicationAvailableAsync(organizationId, applicationId, cancellationToken))
        {
            return true;
        }

        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.DeniedApplicationAccess,
            UserId: await users.GetIdByUidAsync(identityPlatformUid, cancellationToken),
            OrganizationId: organizationId,
            Metadata: JsonSerializer.Serialize(new { applicationId })));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return false;
    }
}
