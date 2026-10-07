using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Applications;

public enum EntitlementOutcome
{
    Changed,

    /// <summary>Already in the requested state. Nothing was written.</summary>
    AlreadyInState,

    OrganizationNotFound,

    /// <summary>No such application in the catalog, or it is retired.</summary>
    ApplicationNotFound,

    /// <summary>No such module of this application, or it is retired.</summary>
    ModuleNotFound,

    /// <summary>A module can only be enabled on an application the organization has.</summary>
    ApplicationNotAssigned,
}

/// <summary>
/// Tenant entitlements, platform administrators only: which applications an
/// organization has (<c>Application.Assign</c>) and which of their modules are
/// on (<c>Module.Manage</c>, licensed, Decision 6).
/// </summary>
/// <remarks>
/// <para>
/// Every change is one transaction with its audit rows, then invalidates the
/// cached permissions of everyone in that organization's application.
/// Assigning an application or enabling a module grants nobody anything
/// (deny-by-default): access comes only from an application administrator's
/// explicit grant.
/// </para>
/// <para>
/// Cascades (ORG-APP-MODULE-MODEL.md, section 8):
/// <list type="bullet">
/// <item>Removing an application hard-revokes every grant for it in the
/// organization (Decision 5), each audited <c>access_revoked</c>. Re-assigning
/// restores the application and its module selections, but no access.</item>
/// <item>Disabling a module keeps every grant; its permissions just stop
/// resolving. Re-enabling restores access as it was.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class OrganizationApplicationService(
    IOrganizationRepository organizations,
    IApplicationRepository applications,
    IRoleRepository roles,
    IAuthorizationQueries queries,
    IUserRepository users,
    IPermissionCache permissionCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public Task<IReadOnlyList<OrganizationApplicationDto>> ListForOrganizationAsync(int organizationId, CancellationToken cancellationToken) =>
        queries.ListOrganizationApplicationsAsync(organizationId, cancellationToken);

    public async Task<EntitlementOutcome> AssignAsync(string actorUid, int organizationId, int applicationId, CancellationToken cancellationToken)
    {
        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);

        EntitlementOutcome outcome;
        try
        {
            outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await organizations.GetByIdAsync(organizationId, ct) is null)
                {
                    return EntitlementOutcome.OrganizationNotFound;
                }

                if (await applications.FindCatalogApplicationAsync(applicationId, ct) is not { IsActive: true })
                {
                    return EntitlementOutcome.ApplicationNotFound;
                }

                var now = timeProvider.GetUtcNow();
                switch (await applications.FindOrganizationApplicationAsync(organizationId, applicationId, ct))
                {
                    case { IsActive: true }:
                        return EntitlementOutcome.AlreadyInState;
                    case { } removed:
                        removed.Reassign(now);
                        break;
                    default:
                        applications.AddOrganizationApplication(OrganizationApplication.Assign(organizationId, applicationId, now));
                        break;
                }

                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.ApplicationAssigned,
                    UserId: actorUserId,
                    OrganizationId: organizationId,
                    Metadata: JsonSerializer.Serialize(new { applicationId })));
                await unitOfWork.SaveChangesAsync(ct);

                return EntitlementOutcome.Changed;
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.OrganizationApplication)
        {
            return EntitlementOutcome.AlreadyInState;
        }

        if (outcome == EntitlementOutcome.Changed)
        {
            await permissionCache.InvalidateOrganizationApplicationAsync(organizationId, applicationId, cancellationToken);
        }

        return outcome;
    }

    public async Task<EntitlementOutcome> RemoveAsync(string actorUid, int organizationId, int applicationId, CancellationToken cancellationToken)
    {
        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);
        var revokedScopes = new List<PermissionScope>();

        var outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await applications.FindOrganizationApplicationAsync(organizationId, applicationId, ct) is not { IsActive: true } organizationApplication)
            {
                return EntitlementOutcome.AlreadyInState;
            }

            var grants = await roles.ListApplicationGrantsAsync(organizationId, applicationId, ct);
            foreach (var grant in grants)
            {
                roles.RemoveUserRole(grant);
                revokedScopes.Add(new PermissionScope(grant.UserId, organizationId, applicationId));
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.AccessRevoked,
                    UserId: actorUserId,
                    OrganizationId: organizationId,
                    Metadata: JsonSerializer.Serialize(new
                    {
                        targetUserId = grant.UserId,
                        roleId = grant.RoleId,
                        organizationId,
                        applicationId,
                        reason = IdentityAuditEventTypes.ApplicationRemoved,
                    })));
            }

            organizationApplication.Remove(timeProvider.GetUtcNow());
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.ApplicationRemoved,
                UserId: actorUserId,
                OrganizationId: organizationId,
                Metadata: JsonSerializer.Serialize(new { applicationId, revokedGrantCount = grants.Count })));
            await unitOfWork.SaveChangesAsync(ct);

            return EntitlementOutcome.Changed;
        }, cancellationToken);

        if (outcome == EntitlementOutcome.Changed)
        {
            await permissionCache.InvalidateOrganizationApplicationAsync(organizationId, applicationId, cancellationToken);
            foreach (var scope in revokedScopes.Distinct())
            {
                await permissionCache.InvalidateAsync(scope, cancellationToken);
            }
        }

        return outcome;
    }

    public async Task<EntitlementOutcome> EnableModuleAsync(
        string actorUid, int organizationId, int applicationId, int moduleId, CancellationToken cancellationToken)
    {
        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);

        EntitlementOutcome outcome;
        try
        {
            outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await applications.FindCatalogModuleAsync(applicationId, moduleId, ct) is not { IsActive: true })
                {
                    return EntitlementOutcome.ModuleNotFound;
                }

                if (await applications.FindOrganizationApplicationAsync(organizationId, applicationId, ct) is not { IsActive: true } organizationApplication)
                {
                    return EntitlementOutcome.ApplicationNotAssigned;
                }

                var now = timeProvider.GetUtcNow();
                switch (await applications.FindOrganizationApplicationModuleAsync(organizationApplication.Id, moduleId, ct))
                {
                    case { IsEnabled: true }:
                        return EntitlementOutcome.AlreadyInState;
                    case { } disabled:
                        disabled.Reenable(now);
                        break;
                    default:
                        applications.AddOrganizationApplicationModule(
                            OrganizationApplicationModule.Enable(organizationApplication.Id, applicationId, moduleId, now));
                        break;
                }

                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.ModuleEnabled,
                    UserId: actorUserId,
                    OrganizationId: organizationId,
                    Metadata: JsonSerializer.Serialize(new { applicationId, moduleId })));
                await unitOfWork.SaveChangesAsync(ct);

                return EntitlementOutcome.Changed;
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.OrganizationApplicationModule)
        {
            return EntitlementOutcome.AlreadyInState;
        }

        if (outcome == EntitlementOutcome.Changed)
        {
            await permissionCache.InvalidateOrganizationApplicationAsync(organizationId, applicationId, cancellationToken);
        }

        return outcome;
    }

    public async Task<EntitlementOutcome> DisableModuleAsync(
        string actorUid, int organizationId, int applicationId, int moduleId, CancellationToken cancellationToken)
    {
        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);

        var outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await applications.FindCatalogModuleAsync(applicationId, moduleId, ct) is null)
            {
                return EntitlementOutcome.ModuleNotFound;
            }

            if (await applications.FindOrganizationApplicationAsync(organizationId, applicationId, ct) is not { } organizationApplication
                || await applications.FindOrganizationApplicationModuleAsync(organizationApplication.Id, moduleId, ct) is not { IsEnabled: true } enabled)
            {
                return EntitlementOutcome.AlreadyInState;
            }

            enabled.Disable(timeProvider.GetUtcNow());
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.ModuleDisabled,
                UserId: actorUserId,
                OrganizationId: organizationId,
                Metadata: JsonSerializer.Serialize(new { applicationId, moduleId })));
            await unitOfWork.SaveChangesAsync(ct);

            return EntitlementOutcome.Changed;
        }, cancellationToken);

        if (outcome == EntitlementOutcome.Changed)
        {
            await permissionCache.InvalidateOrganizationApplicationAsync(organizationId, applicationId, cancellationToken);
        }

        return outcome;
    }
}
