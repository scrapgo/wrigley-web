using ScrapGo.Core.Modules.Identity.Application.Features;

namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

public interface IFeatureRepository
{
    /// <summary>Every feature with its permissions and targets, by name.</summary>
    Task<IReadOnlyList<FeatureDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>One feature as <see cref="ListAsync"/> returns it, or null.</summary>
    Task<FeatureDto?> GetAsync(int featureId, CancellationToken cancellationToken);

    /// <summary>A tracked feature, or null.</summary>
    Task<Feature?> FindAsync(int featureId, CancellationToken cancellationToken);

    /// <summary>The feature's targets, tracked, for changing them.</summary>
    Task<IReadOnlyList<FeatureTarget>> ListTargetsAsync(int featureId, CancellationToken cancellationToken);

    void AddTargets(IEnumerable<FeatureTarget> targets);

    void RemoveTargets(IEnumerable<FeatureTarget> targets);

    /// <summary>How many of these user ids exist.</summary>
    Task<int> CountUsersAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken);
}
