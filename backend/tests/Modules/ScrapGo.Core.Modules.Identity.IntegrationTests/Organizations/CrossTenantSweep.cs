// ADMIN-API-STATUS Task 10: every org-scoped endpoint, called by org A's administrator against
// org B's ids, is denied (403 or 404), leaks nothing about org B, and changes nothing in org B.
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
        int OutsiderId);

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
        ["POST org B role permission"] = new(HttpMethod.Post, t => $"/api/roles/{t.RoleB}/permissions", _ => new { permissionName = Permissions.ReportRead }),
        ["DELETE org B role permission"] = new(HttpMethod.Delete, t => $"/api/roles/{t.RoleB}/permissions/{Permissions.InvoiceRead}", _ => null),
        ["POST assign org B role in org B"] = new(HttpMethod.Post, t => $"/api/users/{t.MemberB}/roles", t => new { roleId = t.RoleB, organizationId = t.OrgB }),
        ["POST assign org B role in org A"] = new(HttpMethod.Post, t => $"/api/users/{t.MemberB}/roles", t => new { roleId = t.RoleB, organizationId = t.OrgA }),
        ["DELETE revoke org B role in org B"] = new(HttpMethod.Delete, t => $"/api/users/{t.MemberB}/roles/{t.RoleB}?organizationId={t.OrgB}", _ => null),
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
            new { permissionName = Permissions.InvoiceRead })).EnsureSuccessStatusCode();

        var memberBEmail = $"member-{Guid.NewGuid():N}@org-b.example";
        var (_, memberB) = await fixture.SeedUserAsync(memberBEmail);
        await fixture.SeedMembershipAsync(memberB, orgB);
        await fixture.AssignRoleAsync(memberB, roleB, orgB);

        var (_, outsiderId) = await fixture.SeedUserAsync();

        return new(adminAUid, orgA, orgB, orgBName, roleB, roleBName, memberB, memberBEmail, outsiderId);
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

        return string.Join(" / ", organization, string.Join(",", members), string.Join(",", roles),
            string.Join(",", grants), string.Join(",", assignments), outsiderMemberships);
    }
}
