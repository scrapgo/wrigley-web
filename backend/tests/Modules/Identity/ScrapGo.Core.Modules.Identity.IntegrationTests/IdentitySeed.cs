namespace ScrapGo.Core.Modules.Identity.IntegrationTests;

/// <summary>
/// Arranges identity/RBAC rows directly through the real DbContext, using the
/// domain factories, so scenarios read as intent ("an org admin", "a member
/// holding Invoice.Read in org A") rather than row plumbing.
/// </summary>
public static class IdentitySeed
{
    public static async Task<(string Uid, int UserId)> SeedUserAsync(this IdentitySpecFixture fixture, string email = "member@example.com")
    {
        var uid = Guid.NewGuid().ToString();
        var user = User.Provision(uid, email, UserClassification.External, DateTimeOffset.UtcNow);
        fixture.DbContext.Users.Add(user);
        await fixture.DbContext.SaveChangesAsync();

        return (uid, user.Id);
    }

    public static async Task<int> SeedOrganizationAsync(this IdentitySpecFixture fixture, bool disabled = false)
    {
        var now = DateTimeOffset.UtcNow;
        var organization = Organization.Create($"Org {Guid.NewGuid():N}", now);
        if (disabled)
        {
            organization.Disable(now);
        }

        fixture.DbContext.Organizations.Add(organization);
        await fixture.DbContext.SaveChangesAsync();

        return organization.Id;
    }

    public static async Task SeedMembershipAsync(this IdentitySpecFixture fixture, int userId, int organizationId, bool disabled = false)
    {
        var now = DateTimeOffset.UtcNow;
        var membership = OrganizationMembership.Create(userId, organizationId, now);
        if (disabled)
        {
            membership.Disable(now);
        }

        fixture.DbContext.OrganizationMemberships.Add(membership);
        await fixture.DbContext.SaveChangesAsync();
    }

    /// <summary>A new user with an active membership in a new organization, and no roles.</summary>
    public static async Task<(string Uid, int UserId, int OrganizationId)> SeedMemberAsync(this IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync();
        var organizationId = await fixture.SeedOrganizationAsync();
        await fixture.SeedMembershipAsync(userId, organizationId);

        return (uid, userId, organizationId);
    }

    /// <summary>A member of a new organization holding the built-in OrganizationAdministrator role there.</summary>
    public static async Task<(string Uid, int UserId, int OrganizationId)> SeedOrganizationAdministratorAsync(this IdentitySpecFixture fixture)
    {
        var (uid, userId, organizationId) = await fixture.SeedMemberAsync();

        var administratorRoleId = await fixture.DbContext.Roles
            .Where(r => r.Name == DefaultRoleNames.OrganizationAdministrator && r.OrganizationId == null)
            .Select(r => r.Id)
            .SingleAsync();

        await fixture.AssignRoleAsync(userId, administratorRoleId, organizationId);

        return (uid, userId, organizationId);
    }

    /// <summary>
    /// Creates a role granting <paramref name="permissionNames"/> and assigns it
    /// to the user. It is organization-scoped when <paramref name="organizationId"/>
    /// is set, and platform-scoped (role and assignment both have no
    /// organization) when it is null.
    /// </summary>
    public static async Task<int> GrantPermissionsAsync(
        this IdentitySpecFixture fixture, int userId, int? organizationId, params string[] permissionNames)
    {
        var now = DateTimeOffset.UtcNow;
        var roleName = $"Spec Role {Guid.NewGuid():N}";
        var role = organizationId is { } id
            ? Role.CreateForOrganization(id, roleName, null, now)
            : Role.CreatePlatformRole(roleName, null, now);

        fixture.DbContext.Roles.Add(role);
        await fixture.DbContext.SaveChangesAsync();

        var permissionIds = await fixture.DbContext.Permissions
            .Where(p => permissionNames.Contains(p.Name))
            .Select(p => p.Id)
            .ToListAsync();

        fixture.DbContext.RolePermissions.AddRange(permissionIds.Select(permissionId => RolePermission.Create(role.Id, permissionId)));
        await fixture.DbContext.SaveChangesAsync();

        await fixture.AssignRoleAsync(userId, role.Id, organizationId);

        return role.Id;
    }

    public static async Task AssignRoleAsync(this IdentitySpecFixture fixture, int userId, int roleId, int? organizationId)
    {
        fixture.DbContext.UserRoles.Add(UserRole.Assign(userId, roleId, organizationId, DateTimeOffset.UtcNow));
        await fixture.DbContext.SaveChangesAsync();
    }
}
