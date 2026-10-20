using ScrapGo.Core.Modules.Identity.Application.Access;
using ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Access;

/// <summary>
/// Loads an <see cref="AccessContext"/> with one EF query (the user, their
/// roles, their roles' permissions, every feature's state and whether it
/// targets them, and their supplier links), then applies the access rule.
/// </summary>
public sealed class EfAccessContextProvider(IdentityDbContext dbContext) : IAccessContextProvider
{
    public async Task<AccessContext?> LoadAsync(string identityPlatformUid, CancellationToken cancellationToken)
    {
        var row = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.IdentityPlatformUid == identityPlatformUid)
            .Select(u => new
            {
                u.Id,
                u.UserType,
                u.Status,
                Roles = dbContext.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Select(ur => ur.Role.Key)
                    .ToList(),
                Grants = dbContext.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .SelectMany(ur => ur.Role.RolePermissions.Select(rp => new { rp.Permission.Key, rp.Permission.FeatureId }))
                    .ToList(),
                Features = dbContext.Features
                    .Select(f => new
                    {
                        f.Id,
                        f.Key,
                        f.State,
                        // A Targeted feature is on when the user, or one of their roles, is a target.
                        IsTarget = dbContext.FeatureTargets.Any(t => t.FeatureId == f.Id
                            && (t.UserId == u.Id || dbContext.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == t.RoleId))),
                    })
                    .ToList(),
                SupplierRecordIds = dbContext.UserSuppliers
                    .Where(us => us.UserId == u.Id)
                    .Select(us => us.SupplierRecordId)
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var featuresOn = row.Features
            .Where(f => f.State == FeatureState.On || (f.State == FeatureState.Targeted && f.IsTarget))
            .ToList();
        var featureIdsOn = featuresOn.Select(f => f.Id).ToHashSet();

        // A toggle never grants access: it only exposes permissions a role already grants.
        var permissions = row.Status == UserStatus.Active
            ? row.Grants.Where(g => featureIdsOn.Contains(g.FeatureId)).Select(g => g.Key).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        return new AccessContext(
            row.Id,
            row.UserType,
            row.Status,
            [.. row.Roles.Order(StringComparer.Ordinal)],
            permissions,
            featuresOn.Select(f => f.Key).ToHashSet(StringComparer.Ordinal),
            row.SupplierRecordIds.ToHashSet());
    }
}
