// /api/suppliers: the same reads without an organization or application,
// for platform administrators only (Admin.Access at platform scope).
using ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class ReadingSuppliersPlatformWide
{
    public class Given_a_platform_administrator(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Get_by_record_id_returns_the_supplier()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.FirstClassAutoSalvage);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers/17511", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("{3.EX.'17511'}", fixture.Quickbase.Queries.Last().Where);
            var supplier = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("supplier");
            Assert.Equal("1st Class Auto Salvage", supplier.GetProperty("account").GetString());
        }

        [Fact]
        public async Task The_list_pages_and_searches()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.NameList);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers?search=Auto&top=10", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var query = fixture.Quickbase.Queries.Last();
            Assert.Equal("{8.XEX.''}AND{8.CT.'Auto'}", query.Where);
            Assert.Equal(10, query.Top);
            Assert.Equal(4, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").GetArrayLength());
        }

        [Fact]
        public async Task An_unknown_record_returns_four_hundred_four()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.Empty);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers/99999999", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("supplier_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        // Platform access needs a Google Workspace sign-in on every request.
        [Fact]
        public async Task Without_a_workspace_sign_in_it_returns_four_hundred_three()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers", fixture.CreateToken(uid, withoutHostedDomain: true));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("missing_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }
    }

    public class Given_a_caller_who_is_not_a_platform_administrator(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        // Even holding Downstream.Suppliers.Read in an organization: that grant is
        // scoped to the organization's application route, not the platform one.
        [Fact]
        public async Task An_organization_supplier_reader_gets_four_hundred_three_and_no_query_runs()
        {
            var (token, _) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);
            var before = fixture.Quickbase.Queries.Count;

            var list = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers", token);
            var single = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers/17511", token);

            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, single.StatusCode);
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }

        [Fact]
        public async Task An_anonymous_caller_gets_four_hundred_one()
        {
            var response = await fixture.Client.GetAsync("/api/suppliers");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
