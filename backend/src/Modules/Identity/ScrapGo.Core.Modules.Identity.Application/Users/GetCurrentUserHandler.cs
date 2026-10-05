using ScrapGo.Core.Modules.Identity.Application.Authorization;

namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>
/// <c>GET /api/users/me</c>: provisions the caller on first sight, then adds
/// their effective roles and permissions from <see cref="PermissionResolver"/>,
/// the same source the authorization handler decides with.
/// </summary>
public sealed class GetCurrentUserHandler(ProvisionCurrentUserHandler provisionCurrentUser, PermissionResolver permissionResolver)
{
    public async Task<CurrentUserDto> HandleAsync(ProvisionCurrentUserCommand command, CancellationToken cancellationToken)
    {
        var user = await provisionCurrentUser.HandleAsync(command, cancellationToken);
        var access = await permissionResolver.GetEffectiveAccessAsync(user.Id, cancellationToken);

        return user with { Roles = access.Roles, Permissions = access.Permissions };
    }
}
