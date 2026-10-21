using ScrapGo.Core.Modules.Identity.Application.Features;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class FeatureRepository(IdentityDbContext dbContext) : IFeatureRepository
{
    public async Task<IReadOnlyList<FeatureDto>> ListAsync(CancellationToken cancellationToken) =>
        [.. (await QueryFeatures(dbContext.Features.AsNoTracking()).ToListAsync(cancellationToken))
            .OrderBy(row => row.Feature.Name, StringComparer.Ordinal)
            .Select(ToDto)];

    public async Task<FeatureDto?> GetAsync(int featureId, CancellationToken cancellationToken) =>
        await QueryFeatures(dbContext.Features.AsNoTracking().Where(f => f.Id == featureId)).SingleOrDefaultAsync(cancellationToken) is { } row
            ? ToDto(row)
            : null;

    public Task<Feature?> FindAsync(int featureId, CancellationToken cancellationToken) =>
        dbContext.Features.SingleOrDefaultAsync(f => f.Id == featureId, cancellationToken);

    public async Task<IReadOnlyList<FeatureTarget>> ListTargetsAsync(int featureId, CancellationToken cancellationToken) =>
        await dbContext.FeatureTargets.Where(t => t.FeatureId == featureId).ToListAsync(cancellationToken);

    public void AddTargets(IEnumerable<FeatureTarget> targets) => dbContext.FeatureTargets.AddRange(targets);

    public void RemoveTargets(IEnumerable<FeatureTarget> targets) => dbContext.FeatureTargets.RemoveRange(targets);

    public Task<int> CountUsersAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().CountAsync(u => userIds.Contains(u.Id), cancellationToken);

    private IQueryable<FeatureRow> QueryFeatures(IQueryable<Feature> features) =>
        features.Select(f => new FeatureRow(
            f,
            dbContext.Permissions.Where(p => p.FeatureId == f.Id).Select(p => p.Key).ToList(),
            dbContext.FeatureTargets.Where(t => t.FeatureId == f.Id && t.UserId != null).Select(t => t.UserId!.Value).ToList(),
            dbContext.FeatureTargets.Where(t => t.FeatureId == f.Id && t.RoleId != null).Select(t => t.RoleId!.Value).ToList()));

    private static FeatureDto ToDto(FeatureRow row) =>
        new(
            row.Feature.Id,
            row.Feature.Key,
            row.Feature.Name,
            row.Feature.Description,
            row.Feature.State.ToString(),
            row.Feature.IsToggleable,
            row.Feature.UpdatedAt,
            row.Feature.UpdatedByUserId,
            [.. row.Permissions.Order(StringComparer.Ordinal)],
            new FeatureTargetsDto([.. row.UserIds.Order()], [.. row.RoleIds.Order()]));

    private sealed record FeatureRow(Feature Feature, List<string> Permissions, List<int> UserIds, List<int> RoleIds);
}
