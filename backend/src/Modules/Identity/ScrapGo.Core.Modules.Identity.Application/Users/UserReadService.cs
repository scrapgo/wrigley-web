using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <param name="Status">Optional; must name a <see cref="UserStatus"/> (case-insensitive).</param>
public sealed record ListUsersQuery(string? Search, string? Status, int? Page, int? PageSize);

public enum UserReadOutcome
{
    Success,

    /// <summary>A page value out of range, or an unknown status.</summary>
    InvalidRequest,

    NotFound,
}

public sealed record UserListResult(UserReadOutcome Outcome, PagedResult<UserSummaryDto>? Users = null);

public sealed record UserDetailResult(UserReadOutcome Outcome, UserDetailDto? User = null);

/// <summary>
/// Platform-wide user reads for the admin portal.
/// </summary>
/// <remarks>
/// Every user on the platform is visible here, so the routes require
/// <c>User.Read</c> at platform scope (<c>[RequirePermission(..., PlatformScope = true)]</c>),
/// checked before these methods run. Organization admins see their own
/// organization's people through the organization member routes, never here.
/// </remarks>
public sealed class UserReadService(IAuthorizationQueries authorization, IUserRepository users, IRoleRepository roles)
{
    public async Task<UserListResult> ListAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        if (!page.IsValid || !TryParseStatus(query.Status, out var status))
        {
            return new(UserReadOutcome.InvalidRequest);
        }

        var result = await authorization.ListUsersAsync(new UserListFilter(query.Search, status), page, cancellationToken);

        return new(UserReadOutcome.Success, result);
    }

    public async Task<UserDetailResult> GetAsync(int userId, CancellationToken cancellationToken)
    {
        if (await users.GetByIdAsync(userId, cancellationToken) is not { } user)
        {
            return new(UserReadOutcome.NotFound);
        }

        return new(UserReadOutcome.Success, UserDetailDto.From(user, await roles.ListUserRolesAsync(userId, cancellationToken)));
    }

    /// <summary>Names only ("Active", "disabled"); numeric strings are rejected rather than cast to the enum.</summary>
    private static bool TryParseStatus(string? value, out UserStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.Any(char.IsDigit) || !Enum.TryParse<UserStatus>(value, ignoreCase: true, out var parsed))
        {
            return false;
        }

        status = parsed;
        return true;
    }
}
