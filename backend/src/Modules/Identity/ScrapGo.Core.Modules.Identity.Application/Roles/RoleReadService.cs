namespace ScrapGo.Core.Modules.Identity.Application.Roles;

/// <summary>
/// Role reads within one organization: its own active custom roles plus the
/// built-in platform-defined roles.
/// </summary>
/// <remarks>
/// Authorization happens before these methods run, on the route's
/// <c>{organizationId}</c>: the membership guard (active membership) and
/// <c>[RequirePermission(Role.Read)]</c>. Isolation is still enforced here,
/// in the query: every lookup is filtered to roles visible from that
/// organization, so a role id from another organization is NotFound.
/// </remarks>
public sealed class RoleReadService(IAuthorizationQueries authorization, IRoleRepository roles)
{
    public Task<IReadOnlyList<RoleDto>> ListAsync(int organizationId, CancellationToken cancellationToken) =>
        authorization.ListRolesAsync(organizationId, cancellationToken);

    public async Task<RoleDetailResult> GetAsync(int organizationId, int roleId, CancellationToken cancellationToken)
    {
        if (await authorization.FindRoleAsync(organizationId, roleId, cancellationToken) is not { } role)
        {
            return new(RoleReadOutcome.NotFound);
        }

        var permissions = await roles.GetRolePermissionsAsync(role.Id, cancellationToken);

        return new(RoleReadOutcome.Success,
            new RoleDetailDto(role.Id, role.Name, role.Description, role.OrganizationId, permissions));
    }

    public async Task<RolePermissionsResult> GetPermissionsAsync(int organizationId, int roleId, CancellationToken cancellationToken) =>
        await authorization.FindRoleAsync(organizationId, roleId, cancellationToken) is { } role
            ? new(RoleReadOutcome.Success, await roles.GetRolePermissionsAsync(role.Id, cancellationToken))
            : new(RoleReadOutcome.NotFound);
}
