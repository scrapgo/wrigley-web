using System.Text.Json;
using ScrapGo.Core.Modules.Identity.Application.Authorization;

namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <param name="ActorUid">The caller's UID from the validated token's <c>sub</c> claim.</param>
/// <param name="UserId">The user losing the role, from the route.</param>
/// <param name="OrganizationId">The scope to revoke in, from the query string; null is platform scope.</param>
public sealed record RevokeRoleCommand(string ActorUid, int UserId, int RoleId, int? OrganizationId);

public enum AssignRoleOutcome
{
    Assigned,

    /// <summary>The user already held the role in that scope. Nothing was written.</summary>
    AlreadyAssigned,

    /// <summary>No role id, or a non-positive id.</summary>
    InvalidRequest,

    /// <summary>Organization scope: the caller has no active membership there, or lacks <c>Role.Assign</c> there.</summary>
    Forbidden,

    /// <summary>Platform scope: only a platform administrator (<c>Admin.Access</c> and <c>Role.Assign</c> at platform scope) may assign there.</summary>
    PlatformAdminRequired,

    UserNotFound,

    /// <summary>No such active role.</summary>
    RoleNotFound,

    /// <summary>
    /// The role belongs to another organization than the requested scope.
    /// Reported like <see cref="RoleNotFound"/> (404), so other organizations'
    /// role ids can't be probed.
    /// </summary>
    RoleNotInOrganization,

    /// <summary>A built-in role in the wrong kind of scope: PlatformAdministrator per organization, or OrganizationAdministrator at platform scope.</summary>
    RoleScopeMismatch,

    /// <summary>Organization scope: the target user has no active membership in that organization.</summary>
    TargetNotMember,

    /// <summary>Escalation guard: the role grants a permission the caller doesn't hold in that scope.</summary>
    CannotGrantUnheldPermission,

    /// <summary>External users never hold platform-scoped roles (ORG-APP-MODULE-MODEL.md section 8.1).</summary>
    ExternalUserNotAllowed,
}

public sealed record AssignRoleResult(AssignRoleOutcome Outcome, AssignedRoleDto? Role = null);

public enum RevokeRoleOutcome
{
    Revoked,

    /// <summary>The user didn't hold the role in that scope. Nothing was written.</summary>
    NotAssigned,

    InvalidRequest,

    /// <inheritdoc cref="AssignRoleOutcome.Forbidden"/>
    Forbidden,

    /// <inheritdoc cref="AssignRoleOutcome.PlatformAdminRequired"/>
    PlatformAdminRequired,

    UserNotFound,

    /// <summary>No other active user holds PlatformAdministrator; revoking it would lock everyone out of platform administration.</summary>
    LastPlatformAdministrator,

    /// <summary>No other active member holds OrganizationAdministrator in that organization; revoking it would leave it unmanageable.</summary>
    LastOrganizationAdministrator,
}

/// <summary>
/// Assigns roles to users and revokes them, in one explicit scope: an
/// organization, or the platform when <c>OrganizationId</c> is null.
/// </summary>
/// <remarks>
/// <para>
/// These routes carry no <c>{organizationId}</c>, so authorization is done
/// here, per scope, never from anything the client claims about itself:
/// organization scope needs an active membership and <c>Role.Assign</c> in
/// that organization; platform scope needs <c>Admin.Access</c> and
/// <c>Role.Assign</c> at platform scope. An organization admin can never
/// touch platform scope.
/// </para>
/// <para>
/// Every write runs in one transaction with its audit row, and invalidates
/// the target user's cached permissions for that scope, so the change applies
/// on their next request.
/// </para>
/// </remarks>
public sealed class UserRoleAssignmentService(
    IUserRepository users,
    IRoleRepository roles,
    IAuthorizationQueries authorization,
    PermissionResolver permissionResolver,
    IPermissionCache permissionCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public async Task<AssignRoleResult> AssignAsync(AssignRoleCommand command, CancellationToken cancellationToken)
    {
        if (!command.IsWellFormed)
        {
            return new(AssignRoleOutcome.InvalidRequest);
        }

        var organizationId = command.OrganizationId;
        var roleId = command.RoleId!.Value;

        var authorized = await AuthorizeAsync(command.ActorUid, organizationId, cancellationToken);
        if (authorized.Denied)
        {
            return new(authorized.PlatformScope ? AssignRoleOutcome.PlatformAdminRequired : AssignRoleOutcome.Forbidden);
        }

        if (await users.GetByIdAsync(command.UserId, cancellationToken) is not { } target)
        {
            return new(AssignRoleOutcome.UserNotFound);
        }

        // Customer (external) users may administer their own organization
        // (Decision 4a), never the platform.
        if (organizationId is null && target.Classification == UserClassification.External)
        {
            return new(AssignRoleOutcome.ExternalUserNotAllowed);
        }

        if (await roles.FindActiveRoleAsync(roleId, cancellationToken) is not { } role)
        {
            return new(AssignRoleOutcome.RoleNotFound);
        }

        if (await CheckRoleFitsScopeAsync(role, command.UserId, organizationId, cancellationToken) is { } misfit)
        {
            return new(misfit);
        }

        // Escalation guard: nobody hands out more than they hold in that scope.
        var held = await permissionResolver.GetPermissionNamesAsync(
            new PermissionScope(authorized.ActorUserId, organizationId), cancellationToken);
        var granted = await roles.GetRolePermissionsAsync(role.Id, cancellationToken);
        if (!granted.All(held.Contains))
        {
            return new(AssignRoleOutcome.CannotGrantUnheldPermission);
        }

        return await AssignInTransactionAsync(authorized.ActorUserId, command.UserId, role, organizationId, cancellationToken);
    }

    public async Task<RevokeRoleOutcome> RevokeAsync(RevokeRoleCommand command, CancellationToken cancellationToken)
    {
        if (command.RoleId <= 0 || command.OrganizationId is <= 0)
        {
            return RevokeRoleOutcome.InvalidRequest;
        }

        var authorized = await AuthorizeAsync(command.ActorUid, command.OrganizationId, cancellationToken);
        if (authorized.Denied)
        {
            return authorized.PlatformScope ? RevokeRoleOutcome.PlatformAdminRequired : RevokeRoleOutcome.Forbidden;
        }

        if (await users.GetByIdAsync(command.UserId, cancellationToken) is null)
        {
            return RevokeRoleOutcome.UserNotFound;
        }

        var outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await roles.FindUserRoleAsync(command.UserId, command.RoleId, command.OrganizationId, ct) is not { } assignment)
            {
                return RevokeRoleOutcome.NotAssigned;
            }

            if (command.OrganizationId is null
                && command.RoleId == await roles.GetPlatformRoleIdAsync(DefaultRoleNames.PlatformAdministrator, ct))
            {
                // Locked (as user disable does) so two concurrent changes can't
                // each see "another administrator remains" and remove the last
                // two together. Only active holders count: a disabled one can't act.
                await roles.LockRoleAsync(command.RoleId, ct);
                if (await roles.CountActivePlatformHoldersAsync(command.RoleId, excludingUserId: command.UserId, ct) == 0)
                {
                    return RevokeRoleOutcome.LastPlatformAdministrator;
                }
            }

            if (command.OrganizationId is { } organizationId
                && command.RoleId == await roles.GetPlatformRoleIdAsync(DefaultRoleNames.OrganizationAdministrator, ct))
            {
                // Same lock as membership removal, which enforces the same rule.
                await roles.LockRoleAsync(command.RoleId, ct);
                if (await roles.CountActiveOrganizationHoldersAsync(command.RoleId, organizationId, excludingUserId: command.UserId, ct) == 0)
                {
                    return RevokeRoleOutcome.LastOrganizationAdministrator;
                }
            }

            roles.RemoveUserRole(assignment);
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.RoleRevoked,
                UserId: authorized.ActorUserId,
                OrganizationId: command.OrganizationId,
                Metadata: AuditMetadata(command.UserId, command.RoleId, command.OrganizationId)));
            await unitOfWork.SaveChangesAsync(ct);

            return RevokeRoleOutcome.Revoked;
        }, cancellationToken);

        if (outcome == RevokeRoleOutcome.Revoked)
        {
            await permissionCache.InvalidateAsync(new PermissionScope(command.UserId, command.OrganizationId), cancellationToken);
        }

        return outcome;
    }

    private async Task<AssignRoleResult> AssignInTransactionAsync(
        int actorUserId, int userId, Role role, int? organizationId, CancellationToken cancellationToken)
    {
        var dto = new AssignedRoleDto(role.Id, role.Name, organizationId);

        AssignRoleOutcome outcome;
        try
        {
            outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await roles.FindUserRoleAsync(userId, role.Id, organizationId, ct) is not null)
                {
                    return AssignRoleOutcome.AlreadyAssigned;
                }

                roles.AddUserRole(UserRole.Assign(userId, role.Id, organizationId, timeProvider.GetUtcNow()));
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.RoleAssigned,
                    UserId: actorUserId,
                    OrganizationId: organizationId,
                    Metadata: AuditMetadata(userId, role.Id, organizationId)));
                await unitOfWork.SaveChangesAsync(ct);

                return AssignRoleOutcome.Assigned;
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName is
            IdentityUniqueConstraints.OrganizationRoleAssignment or IdentityUniqueConstraints.PlatformRoleAssignment)
        {
            // A concurrent request assigned it first; its audit row stands, ours rolled back.
            return new(AssignRoleOutcome.AlreadyAssigned, dto);
        }

        if (outcome == AssignRoleOutcome.Assigned)
        {
            await permissionCache.InvalidateAsync(new PermissionScope(userId, organizationId), cancellationToken);
        }

        return new(outcome, dto);
    }

    /// <summary>Null when the role may be assigned to this user in this scope, otherwise why not.</summary>
    private async Task<AssignRoleOutcome?> CheckRoleFitsScopeAsync(
        Role role, int userId, int? organizationId, CancellationToken cancellationToken)
    {
        if (role.OrganizationId is { } roleOrganizationId && roleOrganizationId != organizationId)
        {
            return AssignRoleOutcome.RoleNotInOrganization;
        }

        // Application roles are granted only through the application routes,
        // by application or platform administrators (Decision 3).
        if (role.IsApplicationRole)
        {
            return AssignRoleOutcome.RoleScopeMismatch;
        }

        // Each built-in belongs to one kind of scope only.
        var builtInForOtherScope = organizationId is null
            ? DefaultRoleNames.OrganizationAdministrator
            : DefaultRoleNames.PlatformAdministrator;
        if (role.OrganizationId is null && role.Id == await roles.GetPlatformRoleIdAsync(builtInForOtherScope, cancellationToken))
        {
            return AssignRoleOutcome.RoleScopeMismatch;
        }

        if (organizationId is { } id && !await authorization.HasActiveMembershipAsync(userId, id, cancellationToken))
        {
            return AssignRoleOutcome.TargetNotMember;
        }

        return null;
    }

    /// <summary>
    /// Organization scope: an active membership and <c>Role.Assign</c> there.
    /// Platform scope: <c>Admin.Access</c> and <c>Role.Assign</c> at platform scope.
    /// </summary>
    private async Task<ActorAuthorization> AuthorizeAsync(string actorUid, int? organizationId, CancellationToken cancellationToken)
    {
        var platformScope = organizationId is null;

        if (await users.GetIdByUidAsync(actorUid, cancellationToken) is not { } actorUserId)
        {
            return new(Denied: true, platformScope, ActorUserId: 0);
        }

        var allowed = organizationId is { } id
            ? await authorization.HasActiveMembershipAsync(actorUserId, id, cancellationToken)
                && await permissionResolver.HasPermissionAsync(actorUid, id, Permissions.RoleAssign, cancellationToken)
            : await permissionResolver.HasPermissionAsync(actorUid, null, Permissions.AdminAccess, cancellationToken)
                && await permissionResolver.HasPermissionAsync(actorUid, null, Permissions.RoleAssign, cancellationToken);

        return new(Denied: !allowed, platformScope, actorUserId);
    }

    private static string AuditMetadata(int targetUserId, int roleId, int? organizationId) =>
        JsonSerializer.Serialize(new { targetUserId, roleId, organizationId });

    private readonly record struct ActorAuthorization(bool Denied, bool PlatformScope, int ActorUserId);
}
