using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Features;

/// <summary>
/// Feature toggles (ACCESS-MODEL.md sections 4 and 9). Reads need
/// <c>features.view</c> and changes <c>features.manage</c>, checked on the
/// routes before these methods run.
/// </summary>
/// <remarks>
/// A toggle never grants access; it only exposes a feature to people whose
/// roles already hold its permissions. Locked features (the admin ones) are
/// always On and refuse every change (<see cref="FeatureAdminOutcome.Locked"/>).
/// A change commits with its audit row, then evicts every cached access
/// context, since a feature can affect anyone.
/// </remarks>
public sealed class FeatureAdminService(
    IFeatureRepository features,
    IRoleRepository roles,
    ICurrentAccess currentAccess,
    IAccessCache accessCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public Task<IReadOnlyList<FeatureDto>> ListAsync(CancellationToken cancellationToken) =>
        features.ListAsync(cancellationToken);

    public async Task<FeatureAdminResult> SetStateAsync(SetFeatureStateCommand command, CancellationToken cancellationToken)
    {
        if (!TryParseState(command.State, out var state))
        {
            return new(FeatureAdminOutcome.InvalidRequest);
        }

        if (await features.FindAsync(command.FeatureId, cancellationToken) is not { } feature)
        {
            return new(FeatureAdminOutcome.NotFound);
        }

        if (!feature.IsToggleable)
        {
            return new(FeatureAdminOutcome.Locked);
        }

        if (feature.State != state)
        {
            var actorId = await ActorIdAsync(cancellationToken);
            var previous = feature.State;
            feature.ChangeState(state, actorId, timeProvider.GetUtcNow());
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.FeatureStateChanged,
                UserId: actorId,
                Metadata: JsonSerializer.Serialize(new
                {
                    featureId = feature.Id,
                    featureKey = feature.Key,
                    from = previous.ToString(),
                    to = state.ToString(),
                })));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            accessCache.EvictAll();
        }

        return new(FeatureAdminOutcome.Success, await features.GetAsync(feature.Id, cancellationToken));
    }

    /// <summary>Replaces the feature's targets. They apply while the feature is Targeted, and are kept otherwise.</summary>
    public async Task<FeatureAdminResult> SetTargetsAsync(SetFeatureTargetsCommand command, CancellationToken cancellationToken)
    {
        if (command.UserIds is not { } requestedUsers
            || command.RoleIds is not { } requestedRoles
            || requestedUsers.Any(id => id <= 0)
            || requestedRoles.Any(id => id <= 0))
        {
            return new(FeatureAdminOutcome.InvalidRequest);
        }

        if (await features.FindAsync(command.FeatureId, cancellationToken) is not { } feature)
        {
            return new(FeatureAdminOutcome.NotFound);
        }

        if (!feature.IsToggleable)
        {
            return new(FeatureAdminOutcome.Locked);
        }

        var userIds = requestedUsers.ToHashSet();
        var roleIds = requestedRoles.ToHashSet();
        if (await features.CountUsersAsync(userIds, cancellationToken) != userIds.Count)
        {
            return new(FeatureAdminOutcome.UserNotFound);
        }

        if ((await roles.FindRolesAsync(roleIds, cancellationToken)).Count != roleIds.Count)
        {
            return new(FeatureAdminOutcome.RoleNotFound);
        }

        var actorId = await ActorIdAsync(cancellationToken);
        var changed = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var current = await features.ListTargetsAsync(feature.Id, ct);
            var toRemove = current
                .Where(t => t.UserId is { } u ? !userIds.Contains(u) : !roleIds.Contains(t.RoleId!.Value))
                .ToList();
            var addUsers = userIds.Where(u => current.All(t => t.UserId != u)).Order().ToList();
            var addRoles = roleIds.Where(r => current.All(t => t.RoleId != r)).Order().ToList();
            if (toRemove.Count == 0 && addUsers.Count == 0 && addRoles.Count == 0)
            {
                return false;
            }

            features.RemoveTargets(toRemove);
            features.AddTargets([
                .. addUsers.Select(u => FeatureTarget.ForUser(feature.Id, u)),
                .. addRoles.Select(r => FeatureTarget.ForRole(feature.Id, r)),
            ]);
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.FeatureTargetsChanged,
                UserId: actorId,
                Metadata: JsonSerializer.Serialize(new
                {
                    featureId = feature.Id,
                    featureKey = feature.Key,
                    addedUserIds = addUsers,
                    addedRoleIds = addRoles,
                    removedUserIds = toRemove.Where(t => t.UserId is not null).Select(t => t.UserId!.Value).Order(),
                    removedRoleIds = toRemove.Where(t => t.RoleId is not null).Select(t => t.RoleId!.Value).Order(),
                })));
            await unitOfWork.SaveChangesAsync(ct);

            return true;
        }, cancellationToken);

        if (changed)
        {
            accessCache.EvictAll();
        }

        return new(FeatureAdminOutcome.Success, await features.GetAsync(feature.Id, cancellationToken));
    }

    private async Task<int> ActorIdAsync(CancellationToken cancellationToken) =>
        (await currentAccess.GetAsync(cancellationToken))?.UserId
        ?? throw new InvalidOperationException("An authorized feature-admin request has no access context.");

    /// <summary>Names only; numeric strings are rejected rather than cast to the enum.</summary>
    private static bool TryParseState(string? value, out FeatureState state)
    {
        state = default;
        return !string.IsNullOrWhiteSpace(value)
            && !value.Any(char.IsDigit)
            && Enum.TryParse(value, ignoreCase: true, out state);
    }
}
