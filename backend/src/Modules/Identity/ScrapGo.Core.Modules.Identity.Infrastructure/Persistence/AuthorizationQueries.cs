using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class AuthorizationQueries(IdentityDbContext dbContext) : IAuthorizationQueries
{
    /// <remarks>
    /// Every permission any of the user's roles grants. Feature toggles aren't
    /// applied here yet: the access context (B7) replaces this resolution.
    /// </remarks>
    public async Task<IReadOnlySet<string>> GetPermissionNamesAsync(int userId, CancellationToken cancellationToken)
    {
        var retired = Permissions.Retired;
        var names = await dbContext.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Key))
            // Retired permissions never resolve, even if a stale grant survives.
            .Where(name => !retired.Contains(name))
            .Distinct()
            .ToListAsync(cancellationToken);

        return names.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<PagedResult<UserSummaryDto>> ListUsersAsync(
        UserListFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var users = dbContext.Users.AsNoTracking();

        if (filter.Status is { } status)
        {
            users = users.Where(u => u.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = LikePatterns.Contains(filter.Search.Trim());
            users = users.Where(u => EF.Functions.ILike(u.Email, pattern, LikePatterns.EscapeCharacter));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var rows = await users
            .OrderBy(u => u.Id)
            .Skip(page.Skip)
            .Take(page.ResolvedPageSize)
            .Select(u => new { u.Id, u.Email, u.Status, u.CreatedAt })
            .ToListAsync(cancellationToken);

        return PagedResult<UserSummaryDto>.Create(
            [.. rows.Select(u => new UserSummaryDto(u.Id, u.Email, DisplayName: null, u.Status.ToString(), u.CreatedAt))], page, totalCount);
    }

    public async Task<IReadOnlyList<string>> ListPermissionNamesAsync(CancellationToken cancellationToken) =>
        await dbContext.Permissions
            .AsNoTracking()
            .Where(p => !Permissions.Retired.Contains(p.Key))
            .OrderBy(p => p.Key)
            .Select(p => p.Key)
            .ToListAsync(cancellationToken);
}
