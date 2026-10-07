// ADMIN-API-STATUS Task 10, extended by ORG-APP-MODULE-MODEL Task 13: every org- and application-scoped
// endpoint, called by org A's administrator (also SpecApp's administrator in A) against org B's ids, is
// denied (403 or 404), leaks nothing about org B, and changes nothing in org B.
namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Organizations;

public class CrossTenantSweep(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
{
    private sealed record Tenants(
        string AdminAUid,
        int OrgA,
        int OrgB,
        string OrgBName,
        int RoleB,
        string RoleBName,
        int MemberB,
        string MemberBEmail,
        int OutsiderId,
        int ViewerRoleId);

    private sealed record Case(HttpMethod Method, Func<Tenants, string> Path, Func<Tenants, object?> Body);

    private static readonly Dictionary<string, Case> Cases = new()
    {
        // Routes under org B's {organizationId}: the membership guard.
        ["GET org B detail"] = new(HttpMethod.Get, t => $"/api/organizations/{t.OrgB}", _ => null),
        ["GET org B members"] = new(HttpMethod.Get, t => $"/api/organizations/{t.OrgB}/members", _ => null),
        ["PUT org B"] = new(HttpMethod.Put, t => $"/api/organizations/{t.OrgB}", _ => new { name = "Hijacked" }),
        ["POST org B member"] = new(HttpMethod.Post, t => $"/api/organizations/{t.OrgB}/members/{t.OutsiderId}", _ => null),
        ["DELETE org B member"] = new(HttpMethod.Delete, t => $"/api/organizations/{t.OrgB}/members/{t.MemberB}", _ => null),
        ["GET org B roles"] = new(HttpMethod.Get, t => $"/api/organizations/{t.OrgB}/roles", _ => null),
        ["GET org B role"] = new(HttpMethod.Get, t => $"/api/organizations/{t.OrgB}/roles/{t.RoleB}", _ => null),
        ["GET org B role permissions"] = new(HttpMethod.Get, t => $"/api/organizations/{t.OrgB}/roles/{t.RoleB}/permissions", _ => null),

        // Org B's ids through org A's own routes: not visible from org A.
        ["GET org B role via org A"] = new(HttpMethod.Get, t => $"/api/organizations/{t.OrgA}/roles/{t.RoleB}", _ => null),
        ["GET org B role permissions via org A"] = new(HttpMethod.Get, t => $"/api/organizations/{t.OrgA}/roles/{t.RoleB}/permissions", _ => null),

        // Org context from the body or the stored role: authorized in the service.
        ["POST role in org B"] = new(HttpMethod.Post, _ => "/api/roles", t => new { organizationId = t.OrgB, name = "Planted" }),
        ["PUT org B role"] = new(HttpMethod.Put, t => $"/api/roles/{t.RoleB}", _ => new { name = "Hijacked" }),
        ["DELETE org B role"] = new(HttpMethod.Delete, t => $"/api/roles/{t.RoleB}", _ => null),
        ["POST org B role permission"] = new(HttpMethod.Post, t => $"/api/roles/{t.RoleB}/permissions", _ => new { permissionName = SpecPermissions.Beta }),
        ["DELETE org B role permission"] = new(HttpMethod.Delete, t => $"/api/roles/{t.RoleB}/permissions/{SpecPermissions.Alpha}", _ => null),
        ["POST assign org B role in org B"] = new(HttpMethod.Post, t => $"/api/users/{t.MemberB}/roles", t => new { roleId = t.RoleB, organizationId = t.OrgB }),
        ["POST assign org B role in org A"] = new(HttpMethod.Post, t => $"/api/users/{t.MemberB}/roles", t => new { roleId = t.RoleB, organizationId = t.OrgA }),
        ["DELETE revoke org B role in org B"] = new(HttpMethod.Delete, t => $"/api/users/{t.MemberB}/roles/{t.RoleB}?organizationId={t.OrgB}", _ => null),

        // Applications in org B: the membership guard first, before any application check.
        ["GET org B applications"] = new(HttpMethod.Get, t => $"/api/organizations/{t.OrgB}/applications", _ => null),
        ["GET org B app roles"] = new(HttpMethod.Get, t => $"{AppPath(t.OrgB)}/roles", _ => null),
        ["POST org B app role"] = new(HttpMethod.Post, t => $"{AppPath(t.OrgB)}/roles", _ => new { name = "Planted" }),
        ["PUT org B app grant"] = new(HttpMethod.Put, t => $"{AppPath(t.OrgB)}/members/{t.MemberB}/roles/{t.ViewerRoleId}", _ => new { }),
        ["DELETE org B app grant"] = new(HttpMethod.Delete, t => $"{AppPath(t.OrgB)}/members/{t.MemberB}/roles/{t.ViewerRoleId}", _ => null),
        ["GET org B access review"] = new(HttpMethod.Get, t => $"{AppPath(t.OrgB)}/access", _ => null),
        ["GET org B member grants"] = new(HttpMethod.Get, t => $"/api/organizations/{t.OrgB}/members/{t.MemberB}/grants", _ => null),
        ["POST org B invitation"] = new(HttpMethod.Post, t => $"/api/organizations/{t.OrgB}/invitations", _ => new { email = "planted@evil.example" }),
        ["GET org B invitations"] = new(HttpMethod.Get, t => $"/api/organizations/{t.OrgB}/invitations", _ => null),

        // Org B's member through org A's own route: not a member of A, so not found.
        ["GET org B member grants via org A"] = new(HttpMethod.Get, t => $"/api/organizations/{t.OrgA}/members/{t.MemberB}/grants", _ => null),

        // Platform routes: an org (and app) admin is never a platform admin.
        ["PUT assign app to org B"] = new(HttpMethod.Put, t => $"/api/admin/organizations/{t.OrgB}/applications/{SpecApplications.OtherAppId}", _ => null),
        ["DELETE remove app from org B"] = new(HttpMethod.Delete, t => $"/api/admin/organizations/{t.OrgB}/applications/{SpecApplications.SpecAppId}", _ => null),
        ["DELETE disable org B module"] = new(HttpMethod.Delete, t => $"/api/admin/organizations/{t.OrgB}/applications/{SpecApplications.SpecAppId}/modules/{SpecApplications.AlphaModuleId}", _ => null),
        ["POST deactivate org B"] = new(HttpMethod.Post, t => $"/api/admin/organizations/{t.OrgB}/deactivate", _ => null),
    };

    public static TheoryData<string> CaseNames => [.. Cases.Keys];

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task Org_a_admin_is_denied_and_org_b_is_unchanged(string caseName)
    {
        var tenants = await ArrangeTenantsAsync();
        var before = await SnapshotOrgBAsync(tenants);
        var @case = Cases[caseName];

        var response = await fixture.SendAsync(@case.Method, @case.Path(tenants), fixture.CreateToken(tenants.AdminAUid), @case.Body(tenants));

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(tenants.OrgBName, body);
        Assert.DoesNotContain(tenants.RoleBName, body);
        Assert.DoesNotContain(tenants.MemberBEmail, body);
        Assert.Equal(before, await SnapshotOrgBAsync(tenants));
    }

    /// <summary>Fresh orgs per case, so no case can affect another.</summary>
    private async Task<Tenants> ArrangeTenantsAsync()
    {
        var (adminAUid, _, orgA) = await fixture.SeedOrganizationAdministratorAsync();
        var (adminBUid, _, orgB) = await fixture.SeedOrganizationAdministratorAsync();
        var orgBName = await fixture.DbContext.Organizations.AsNoTracking().Where(o => o.Id == orgB).Select(o => o.Name).SingleAsync();

        var roleBName = $"Org B Secret Role {Guid.NewGuid():N}";
        var roleB = await Roles.CustomRoleCrud.ReadIdAsync(await Roles.CustomRoleCrud.PostRoleAsync(fixture, adminBUid, orgB, roleBName, null));
        (await fixture.SendAsync(HttpMethod.Post, $"/api/roles/{roleB}/permissions", fixture.CreateToken(adminBUid),
            new { permissionName = SpecPermissions.Alpha })).EnsureSuccessStatusCode();

        var memberBEmail = $"member-{Guid.NewGuid():N}@org-b.example";
        var (_, memberB) = await fixture.SeedUserAsync(memberBEmail);
        await fixture.SeedMembershipAsync(memberB, orgB);
        await fixture.AssignRoleAsync(memberB, roleB, orgB);

        var (_, outsiderId) = await fixture.SeedUserAsync();

        // Both orgs have SpecApp; A's admin also administers it in A, and B's member holds Viewer in B.
        var viewerRoleId = await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer);
        var adminAId = await fixture.DbContext.Users.AsNoTracking().Where(u => u.IdentityPlatformUid == adminAUid).Select(u => u.Id).SingleAsync();
        await fixture.EntitleAsync(orgA, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
        await fixture.EntitleAsync(orgB, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
        await fixture.GrantApplicationRoleAsync(
            adminAId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppAdministrator), orgA, SpecApplications.SpecAppId);
        await fixture.GrantApplicationRoleAsync(memberB, viewerRoleId, orgB, SpecApplications.SpecAppId);

        return new(adminAUid, orgA, orgB, orgBName, roleB, roleBName, memberB, memberBEmail, outsiderId, viewerRoleId);
    }

    /// <summary>Everything about org B a cross-tenant write could touch.</summary>
    private async Task<string> SnapshotOrgBAsync(Tenants t)
    {
        var db = fixture.DbContext;
        var organization = await db.Organizations.AsNoTracking().Where(o => o.Id == t.OrgB).Select(o => o.Name + "|" + o.Status).SingleAsync();
        var members = await db.OrganizationMemberships.AsNoTracking()
            .Where(m => m.OrganizationId == t.OrgB).OrderBy(m => m.UserId).Select(m => m.UserId + ":" + m.Status).ToListAsync();
        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.OrganizationId == t.OrgB).OrderBy(r => r.Id).Select(r => r.Id + ":" + r.Name + ":" + r.Status).ToListAsync();
        var grants = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == t.RoleB).OrderBy(rp => rp.PermissionId).Select(rp => rp.PermissionId).ToListAsync();
        var assignments = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.OrganizationId == t.OrgB || ur.RoleId == t.RoleB).OrderBy(ur => ur.Id)
            .Select(ur => ur.UserId + ":" + ur.RoleId + ":" + ur.OrganizationId).ToListAsync();
        var outsiderMemberships = await db.OrganizationMemberships.AsNoTracking().CountAsync(m => m.UserId == t.OutsiderId);
        var applications = await db.OrganizationApplications.AsNoTracking()
            .Where(oa => oa.OrganizationId == t.OrgB).OrderBy(oa => oa.ApplicationId).Select(oa => oa.ApplicationId + ":" + oa.Status).ToListAsync();
        var modules = await db.OrganizationApplicationModules.AsNoTracking()
            .Where(m => db.OrganizationApplications.Any(oa => oa.Id == m.OrganizationApplicationId && oa.OrganizationId == t.OrgB))
            .OrderBy(m => m.ModuleId).Select(m => m.ModuleId + ":" + m.Status).ToListAsync();
        var invitations = await db.Invitations.AsNoTracking().CountAsync(i => i.OrganizationId == t.OrgB);
        var appRoles = await db.Roles.AsNoTracking().CountAsync(r => r.OrganizationId == t.OrgB && r.ApplicationId != null);

        return string.Join(" / ", organization, string.Join(",", members), string.Join(",", roles),
            string.Join(",", grants), string.Join(",", assignments), outsiderMemberships,
            string.Join(",", applications), string.Join(",", modules), invitations, appRoles);
    }

    private static string AppPath(int organizationId) => $"/api/organizations/{organizationId}/applications/{SpecApplications.SpecAppId}";
}
