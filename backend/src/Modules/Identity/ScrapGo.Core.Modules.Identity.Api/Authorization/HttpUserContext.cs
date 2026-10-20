using ScrapGo.Core.Modules.Identity.Application.Access;
using ScrapGo.Core.Modules.Identity.Domain.Users;
using ScrapGo.Core.Shared.Kernel.Security;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// <see cref="IUserContext"/> for an HTTP request. Identity comes from the
/// validated bearer token (<c>sub</c>); status and permissions come from the
/// caller's access context (<see cref="ICurrentAccess"/>), the same one behind
/// <c>[RequirePermission]</c>.
/// </summary>
/// <remarks>Scoped per request. The access context loads at most once per request.</remarks>
public sealed class HttpUserContext(IHttpContextAccessor httpContextAccessor, ICurrentAccess currentAccess) : IUserContext
{
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public string? IdentityPlatformUid => IsAuthenticated ? httpContextAccessor.HttpContext!.User.GetIdentityPlatformUid() : null;

    public async Task<bool> IsActiveUserAsync(CancellationToken cancellationToken) =>
        IsAuthenticated && await currentAccess.GetAsync(cancellationToken) is { Status: UserStatus.Active };

    public async Task<bool> HasPermissionAsync(string permissionName, CancellationToken cancellationToken) =>
        IsAuthenticated && await currentAccess.GetAsync(cancellationToken) is { } access && access.Can(permissionName);
}
