// A supplier's yard capabilities: thirteen Quickbase checkboxes.
namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class YardCapabilitiesSpecs
{
    public class Given_a_supplier_reader(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Sends_the_yard_query_and_maps_every_checkbox()
        {
            var (token, organizationId) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);
            fixture.Quickbase.RespondWith(SupplierResponses.YardCapabilities);

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, "/17511/yard-capabilities"), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var query = fixture.Quickbase.Queries.Last();
            Assert.Equal("bqrcgnatz", query.TableId);
            Assert.Equal([3, 65, 186, 78, 182, 225, 204, 205, 185, 359, 183, 187, 184, 230], query.Select);
            Assert.Equal("{3.EX.'17511'}", query.Where);

            var yard = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("yardCapabilities");
            Assert.Equal(17511, yard.GetProperty("recordId").GetInt32());
            Assert.True(yard.GetProperty("crusherOnSite").GetBoolean());
            Assert.True(yard.GetProperty("loggerOnSite").GetBoolean());
            Assert.True(yard.GetProperty("loadFlatbeds").GetBoolean());
            Assert.False(yard.GetProperty("loadDumps").GetBoolean());
            Assert.False(yard.GetProperty("mobileCrusher").GetBoolean());
            Assert.False(yard.GetProperty("canExport").GetBoolean());
            Assert.True(yard.GetProperty("hasGaylordBoxes").GetBoolean());
            Assert.False(yard.GetProperty("balerOnSite").GetBoolean());
            Assert.True(yard.GetProperty("hasScale").GetBoolean());
            Assert.False(yard.GetProperty("loadVanTrailers").GetBoolean());
            Assert.False(yard.GetProperty("hasLoadWrap").GetBoolean());
            Assert.True(yard.GetProperty("usesOwnTrucks").GetBoolean());
            Assert.False(yard.GetProperty("railAccess").GetBoolean());
        }

        [Fact]
        public async Task An_unknown_record_returns_four_hundred_four()
        {
            var (token, organizationId) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);
            fixture.Quickbase.RespondWith(SupplierResponses.Empty);

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, "/99999999/yard-capabilities"), token);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("supplier_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task Is_platform_wide_for_platform_administrators()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.YardCapabilities);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers/17511/yard-capabilities", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    public class Given_a_caller_without_supplier_access(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task A_member_without_the_permission_gets_four_hundred_three_and_no_query_runs()
        {
            var (uid, _, organizationId) = await fixture.SeedMemberAsync();
            await fixture.EntitleAsync(organizationId, DownstreamApplication.Id, DownstreamApplication.SuppliersModuleId);
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, "/17511/yard-capabilities"), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }

        [Fact]
        public async Task An_organization_reader_gets_four_hundred_three_on_the_platform_route()
        {
            var (token, _) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers/17511/yard-capabilities", token);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
