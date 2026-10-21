namespace ScrapGo.Core.Modules.Identity.Application.Features;

/// <summary>A feature, its toggle and its targets.</summary>
/// <param name="IsToggleable">False for locked features (always On; 409 <c>feature_locked</c> on change).</param>
/// <param name="Permissions">The permission keys the feature owns, ordered.</param>
/// <param name="Targets">Who a Targeted feature is on for. Kept when the state changes, so switching back restores them.</param>
public sealed record FeatureDto(
    int Id,
    string Key,
    string Name,
    string Description,
    string State,
    bool IsToggleable,
    DateTimeOffset UpdatedAt,
    int? UpdatedByUserId,
    IReadOnlyList<string> Permissions,
    FeatureTargetsDto Targets);

/// <param name="UserIds">Targeted users, ascending.</param>
/// <param name="RoleIds">Targeted roles (every holder), ascending.</param>
public sealed record FeatureTargetsDto(IReadOnlyList<int> UserIds, IReadOnlyList<int> RoleIds);

/// <param name="State">Off, On or Targeted (case-insensitive name).</param>
public sealed record SetFeatureStateCommand(int FeatureId, string? State);

/// <param name="UserIds">Every user the feature should target; others are removed. Empty clears them.</param>
/// <param name="RoleIds">Every role the feature should target; others are removed. Empty clears them.</param>
public sealed record SetFeatureTargetsCommand(int FeatureId, IReadOnlyList<int>? UserIds, IReadOnlyList<int>? RoleIds);

public enum FeatureAdminOutcome
{
    /// <summary>Done, or already so (nothing written then).</summary>
    Success,

    /// <summary>An unknown state, a missing list, or a non-positive id.</summary>
    InvalidRequest,

    NotFound,

    /// <summary>The feature is locked: always On, with no targets.</summary>
    Locked,

    /// <summary>One of the user ids doesn't exist.</summary>
    UserNotFound,

    /// <summary>One of the role ids doesn't exist.</summary>
    RoleNotFound,
}

public sealed record FeatureAdminResult(FeatureAdminOutcome Outcome, FeatureDto? Feature = null);
