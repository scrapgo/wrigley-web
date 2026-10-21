using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <param name="Type">Optional; must name a <see cref="UserType"/> (case-insensitive).</param>
/// <param name="Status">Optional; must name a <see cref="UserStatus"/> (case-insensitive).</param>
public sealed record ListUsersQuery(string? Search, string? Type, string? Status, int? Page, int? PageSize);

/// <summary>
/// Admin user reads (<c>GET /api/admin/users</c>). The routes require
/// <c>users.view</c>, checked before these methods run.
/// </summary>
public sealed class UserReadService(IUserRepository users, IRoleRepository roles)
{
    /// <returns>Null for a bad page, type or status (400 <c>invalid_request</c>).</returns>
    public async Task<PagedResult<UserSummaryDto>?> ListAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        if (!page.IsValid
            || !TryParse<UserType>(query.Type, out var type)
            || !TryParse<UserStatus>(query.Status, out var status))
        {
            return null;
        }

        return await users.ListAsync(new UserListFilter(query.Search, status, type), page, cancellationToken);
    }

    /// <returns>Null when there is no such user.</returns>
    public async Task<UserDetailDto?> GetAsync(int userId, CancellationToken cancellationToken)
    {
        if (await users.GetByIdAsync(userId, cancellationToken) is not { } user)
        {
            return null;
        }

        return UserDetailDto.From(
            user,
            await roles.ListUserRolesAsync(userId, cancellationToken),
            await users.ListSupplierRecordIdsAsync(userId, cancellationToken));
    }

    /// <summary>Names only ("Active", "external"); numeric strings are rejected rather than cast to the enum.</summary>
    private static bool TryParse<TEnum>(string? value, out TEnum? parsed)
        where TEnum : struct, Enum
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.Any(char.IsDigit) || !Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
        {
            return false;
        }

        parsed = result;
        return true;
    }
}
