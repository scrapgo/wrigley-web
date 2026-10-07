// CATALOG-AND-ADMIN-GUIDE.md, Part 1, run against the API with the real
// Downstream catalog: the same calls the site makes, in the guide's order.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Applications;

public class DownstreamWalkthrough(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
{
    private const int App = DownstreamApplication.Id;

    private static readonly int[] AllModules =
    [
        DownstreamApplication.PricingModuleId,
        DownstreamApplication.OpportunitiesModuleId,
        DownstreamApplication.LoadsModuleId,
        DownstreamApplication.SuppliersModuleId,
    ];

    [Fact]
    public async Task The_guide_scenario_works_end_to_end()
    {
        var (ericUid, ericId) = await fixture.SeedPlatformAdministratorAsync();
        var (lloydUid, lloydId) = await fixture.SeedUserAsync("lloyd@scrapgo.example", UserClassification.Internal);
        var eric = fixture.CreateToken(ericUid);
        var lloyd = fixture.CreateToken(lloydUid);

        // Step 2: create the organizations (Eric and Lloyd as first administrators).
        var midwest = await CreateOrganizationAsync(eric, "Midwest Metals", ericId);
        var greatLakes = await CreateOrganizationAsync(eric, "Great Lakes Recycling", lloydId);

        // The Applications tab lists Downstream with its four modules and eight permissions.
        var catalog = await ReadAsync(await fixture.SendAsync(HttpMethod.Get, "/api/catalog/applications", eric));
        var downstream = catalog.EnumerateArray().Single(a => a.GetProperty("id").GetInt32() == App);
        Assert.Equal(4, downstream.GetProperty("modules").GetArrayLength());

        // Step 3: assign Downstream to Midwest Metals and enable all four modules.
        await ExpectAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/admin/organizations/{midwest}/applications/{App}", eric);
        foreach (var module in AllModules)
        {
            await ExpectAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/admin/organizations/{midwest}/applications/{App}/modules/{module}", eric);
        }

        var assigned = await ReadAsync(await fixture.SendAsync(HttpMethod.Get, $"/api/admin/organizations/{midwest}/applications", eric));
        Assert.Equal(4, Assert.Single(assigned.EnumerateArray()).GetProperty("modules").GetArrayLength());

        // Step 4: appoint Eric as Downstream Administrator through the platform dialog.
        var roles = await ReadAsync(await fixture.SendAsync(HttpMethod.Get, $"/api/admin/organizations/{midwest}/applications/{App}/roles", eric));
        var administratorRoleId = RoleId(roles, DownstreamApplication.AdministratorRole);
        var viewerRoleId = RoleId(roles, DownstreamApplication.ViewerRole);
        await ExpectAsync(HttpStatusCode.OK, HttpMethod.Put,
            $"/api/admin/organizations/{midwest}/applications/{App}/members/{ericId}/roles/{administratorRoleId}", eric);

        // Step 5: Eric (organization admin) adds Lloyd to Midwest Metals.
        await EnsureSuccessAsync(await fixture.SendAsync(HttpMethod.Post, $"/api/organizations/{midwest}/members/{lloydId}", eric), "add member");

        // Step 6: Eric (application admin) grants Lloyd Downstream Viewer for 30 days.
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
        await ExpectAsync(HttpStatusCode.OK, HttpMethod.Put,
            $"/api/organizations/{midwest}/applications/{App}/members/{lloydId}/roles/{viewerRoleId}", eric, new { expiresAt });

        var access = await AccessAsync(eric, midwest);
        Assert.Contains(Permissions.ApplicationManageAccess, access[ericId]);
        Assert.Equal(
            [DownstreamApplication.LoadsRead, DownstreamApplication.OpportunitiesRead, DownstreamApplication.PricingRead, DownstreamApplication.SuppliersRead],
            access[lloydId].Order(StringComparer.Ordinal));

        // Step 7: turning Suppliers off removes it from Lloyd's effective permissions; on again restores it.
        var suppliers = $"/api/admin/organizations/{midwest}/applications/{App}/modules/{DownstreamApplication.SuppliersModuleId}";
        await ExpectAsync(HttpStatusCode.NoContent, HttpMethod.Delete, suppliers, eric);
        Assert.DoesNotContain(DownstreamApplication.SuppliersRead, (await AccessAsync(eric, midwest))[lloydId]);
        await ExpectAsync(HttpStatusCode.NoContent, HttpMethod.Put, suppliers, eric);
        Assert.Contains(DownstreamApplication.SuppliersRead, (await AccessAsync(eric, midwest))[lloydId]);

        // Step 7: the only Downstream Administrator can't be revoked.
        var revokeLast = await fixture.SendAsync(HttpMethod.Delete,
            $"/api/organizations/{midwest}/applications/{App}/members/{ericId}/roles/{administratorRoleId}", eric);
        Assert.Equal(HttpStatusCode.Conflict, revokeLast.StatusCode);
        Assert.Equal("last_application_administrator", await IdentitySpecFixture.ReadProblemReasonAsync(revokeLast));

        // Step 7: Lloyd's /me shows Downstream in Midwest Metals; Great Lakes Recycling has no applications.
        var me = await ReadAsync(await fixture.SendUsersMeAsync(lloyd));
        var lloydMidwest = me.GetProperty("organizations").EnumerateArray().Single(o => o.GetProperty("organizationId").GetInt32() == midwest);
        Assert.Equal(App, Assert.Single(lloydMidwest.GetProperty("applications").EnumerateArray()).GetProperty("applicationId").GetInt32());
        var greatLakesApps = await ReadAsync(await fixture.SendAsync(HttpMethod.Get, $"/api/organizations/{greatLakes}/applications", lloyd));
        Assert.Equal(0, greatLakesApps.GetArrayLength());

        // Step 7: deactivating blocks members; reactivating restores access.
        await ExpectAsync(HttpStatusCode.NoContent, HttpMethod.Post, $"/api/admin/organizations/{midwest}/deactivate", eric);
        var blocked = await fixture.SendAsync(HttpMethod.Get, $"/api/organizations/{midwest}/applications", eric);
        Assert.Equal("organization_deactivated", await IdentitySpecFixture.ReadProblemReasonAsync(blocked));
        await ExpectAsync(HttpStatusCode.NoContent, HttpMethod.Post, $"/api/admin/organizations/{midwest}/reactivate", eric);
        Assert.Contains(DownstreamApplication.PricingRead, (await AccessAsync(eric, midwest))[lloydId]);

        // Step 7: removing Downstream revokes every grant; assigning it again restores none.
        await ExpectAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/admin/organizations/{midwest}/applications/{App}", eric);
        await ExpectAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/admin/organizations/{midwest}/applications/{App}", eric);
        Assert.False(await fixture.DbContext.UserRoles.AsNoTracking()
            .AnyAsync(ur => ur.OrganizationId == midwest && ur.ApplicationId == App));
    }

    private async Task<int> CreateOrganizationAsync(string token, string name, int firstAdminUserId)
    {
        var response = await fixture.SendAsync(HttpMethod.Post, "/api/admin/organizations", token, new { name, firstAdminUserId });
        await EnsureSuccessAsync(response, $"create {name}");
        return (await ReadAsync(response)).GetProperty("id").GetInt32();
    }

    private async Task<Dictionary<int, string[]>> AccessAsync(string token, int organizationId)
    {
        var json = await ReadAsync(await fixture.SendAsync(HttpMethod.Get, $"/api/organizations/{organizationId}/applications/{App}/access", token));
        return json.EnumerateArray().ToDictionary(
            e => e.GetProperty("userId").GetInt32(),
            e => e.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToArray());
    }

    private async Task ExpectAsync(HttpStatusCode expected, HttpMethod method, string path, string token, object? body = null)
    {
        var response = await fixture.SendAsync(method, path, token, body);
        if (response.StatusCode != expected)
        {
            Assert.Fail($"{method} {path}: expected {(int)expected}, got {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string step)
    {
        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail($"{step}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }
    }

    private static int RoleId(JsonElement roles, string name) =>
        roles.EnumerateArray().Single(r => r.GetProperty("name").GetString() == name).GetProperty("id").GetInt32();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response, response.RequestMessage?.RequestUri?.PathAndQuery ?? "request");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
