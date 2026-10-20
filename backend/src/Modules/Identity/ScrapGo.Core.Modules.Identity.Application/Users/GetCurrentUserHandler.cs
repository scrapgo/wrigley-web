namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>
/// <c>GET /api/users/me</c>: provisions the caller on first sight, then adds
/// their roles and effective permissions from the access context, the same
/// source the authorization handler decides with.
/// </summary>
/// <remarks>
/// The context is loaded straight from the database, not through the
/// request's <c>ICurrentAccess</c>: on a first request the disabled-user gate
/// already saw "no such user" before this handler provisioned one.
/// </remarks>
public sealed class GetCurrentUserHandler(
    ProvisionCurrentUserHandler provisionCurrentUser, IAccessContextProvider accessContexts, IRoleRepository roles)
{
    public async Task<CurrentUserDto> HandleAsync(ProvisionCurrentUserCommand command, CancellationToken cancellationToken)
    {
        var user = await provisionCurrentUser.HandleAsync(command, cancellationToken);
        var access = await accessContexts.LoadAsync(user.IdentityPlatformUid, cancellationToken);

        return user with
        {
            Roles = await roles.ListUserRolesAsync(user.Id, cancellationToken),
            Permissions = access is null ? [] : [.. access.Permissions.Order(StringComparer.Ordinal)],
        };
    }
}
