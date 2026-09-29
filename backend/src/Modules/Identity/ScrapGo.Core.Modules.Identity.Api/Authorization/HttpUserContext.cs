using ScrapGo.Core.Modules.Identity.Application.Abstractions;
using ScrapGo.Core.Modules.Identity.Application.Authorization;
using ScrapGo.Core.Modules.Identity.Domain.Users;
using ScrapGo.Core.Shared.Kernel.Security;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// <see cref="IUserContext"/> for an HTTP request. Identity comes from the
/// validated bearer token (<c>sub</c>), user status from Postgres, and
/// permissions from <see cref="PermissionResolver"/>, the same engine behind
/// <c>[RequirePermission]</c>.
/// </summary>
/// <remarks>Scoped per request. The active-user lookup runs at most once per request.</remarks>
public sealed class HttpUserContext(
    IHttpContextAccessor httpContextAccessor,
    IUserRepository users,
    PermissionResolver permissionResolver) : IUserContext
{
    private Task<bool>? _isActiveUser;

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public string? IdentityPlatformUid => IsAuthenticated ? httpContextAccessor.HttpContext!.User.GetIdentityPlatformUid() : null;

    public Task<bool> IsActiveUserAsync(CancellationToken cancellationToken) =>
        _isActiveUser ??= LoadIsActiveUserAsync(cancellationToken);

    public async Task<bool> HasPermissionAsync(string permissionName, int? organizationId, CancellationToken cancellationToken) =>
        IdentityPlatformUid is { } uid
        && await IsActiveUserAsync(cancellationToken)
        && await permissionResolver.HasPermissionAsync(uid, organizationId, permissionName, cancellationToken);

    private async Task<bool> LoadIsActiveUserAsync(CancellationToken cancellationToken) =>
        IdentityPlatformUid is { } uid
        && await users.GetStatusByUidAsync(uid, cancellationToken) is { Status: UserStatus.Active };
}
