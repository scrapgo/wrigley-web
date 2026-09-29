namespace ScrapGo.Core.Modules.Identity.Application.Organizations;

public enum OrganizationAccessDecision
{
    Allow,
    Deny,
}

/// <summary>
/// The cross-tenant (IDOR) guard: a caller may reach an organization-scoped
/// route only with an active membership in that organization. The
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

        auditLog.Record(new AuditEvent(IdentityAuditEventTypes.DeniedCrossTenantAccess, UserId: userId, OrganizationId: organizationId));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return OrganizationAccessDecision.Deny;
    }
}
