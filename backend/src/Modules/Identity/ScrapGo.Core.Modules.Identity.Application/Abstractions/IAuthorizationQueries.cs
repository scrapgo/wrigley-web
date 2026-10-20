using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

/// <summary>
/// Read-only, untracked queries: those behind every access decision, and the
/// admin list reads, which project straight to DTOs.
/// </summary>
/// <remarks>
/// The list methods assume a valid <see cref="PageRequest"/> (check
/// <see cref="PageRequest.IsValid"/> first) and order by id, so pages are stable.
/// </remarks>
public interface IAuthorizationQueries
{
    /// <summary>Every permission granted by the user's active, unexpired roles.</summary>
    Task<IReadOnlySet<string>> GetPermissionNamesAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Every user on the platform, filtered and paged. Platform-admin only.</summary>
    Task<PagedResult<UserSummaryDto>> ListUsersAsync(UserListFilter filter, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The whole seeded permission catalog, ordered by name.</summary>
    Task<IReadOnlyList<string>> ListPermissionNamesAsync(CancellationToken cancellationToken);
}
