namespace ScrapGo.Core.Modules.Identity.Application.Session;

/// <summary>
/// <c>GET /api/me</c>: the caller's user, roles, effective permissions and
/// features. It never creates a user; that is <see cref="StartSessionHandler"/>'s job.
/// </summary>
public sealed class GetMeHandler(ICurrentAccess currentAccess, IUserRepository users)
{
    /// <returns>Null when the token's UID has no user (403 <c>no_access</c>).</returns>
    public async Task<MeResponse?> HandleAsync(CancellationToken cancellationToken)
    {
        if (await currentAccess.GetAsync(cancellationToken) is not { } access
            || await users.GetByIdAsync(access.UserId, cancellationToken) is not { } user)
        {
            return null;
        }

        return MeResponse.From(user, access);
    }
}
