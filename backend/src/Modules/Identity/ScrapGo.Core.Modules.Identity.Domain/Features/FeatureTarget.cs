namespace ScrapGo.Core.Modules.Identity.Domain.Features;

/// <summary>
/// One target of a <see cref="FeatureState.Targeted"/> feature: exactly one of
/// a user or a role. Unique per (feature, user, role), with nulls not distinct.
/// </summary>
public class FeatureTarget
{
    private FeatureTarget()
    {
    }

    public int Id { get; private set; }

    public int FeatureId { get; private set; }

    public int? UserId { get; private set; }

    public int? RoleId { get; private set; }

    public static FeatureTarget ForUser(int featureId, int userId) => new() { FeatureId = featureId, UserId = userId };

    public static FeatureTarget ForRole(int featureId, int roleId) => new() { FeatureId = featureId, RoleId = roleId };
}
