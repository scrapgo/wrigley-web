// B13: suppliers.view_all sees every supplier; suppliers.view_own only the
// caller's linked Record IDs, filtered in the Quickbase query, and anything
// else is 404 before Quickbase is asked.

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class SupplierScoping
{
    /// <summary>Every per-supplier route: the record and each sub-resource.</summary>
    public static TheoryData<string> SupplierRoutes =>
    [
        "",
        "/target-pricing-progress-rail",
        "/in-stock-items",
        "/contacts",
        "/call-notes-history",
        "/linked-its-dispatch-records",
        "/yard-capabilities",
        "/other-prospect-information",
        "/call-prospect-status",
    ];

    public class Given_an_internal_scrapgo_user(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task The_list_is_not_filtered_by_record_id()
        {
            var token = await fixture.SeedScrapGoUserAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.NameList);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("{8.XEX.''}", fixture.Quickbase.Queries.Last().Where);
        }

        [Fact]
        public async Task Any_supplier_can_be_read()
        {
            var token = await fixture.SeedScrapGoUserAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.FirstClassAutoSalvage);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath("/17511"), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    public class Given_an_external_user_linked_to_two_suppliers(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task The_list_query_is_filtered_to_their_record_ids()
        {
            var token = await fixture.SeedExternalSupplierUserAsync(17511, 9583);
            fixture.Quickbase.RespondWith(SupplierResponses.NameList);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath("?search=Auto"), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("({3.EX.'9583'}OR{3.EX.'17511'})AND{8.XEX.''}AND{8.CT.'Auto'}", fixture.Quickbase.Queries.Last().Where);
        }

        [Fact]
        public async Task A_linked_supplier_can_be_read()
        {
            var token = await fixture.SeedExternalSupplierUserAsync(17511, 9583);
            fixture.Quickbase.RespondWith(SupplierResponses.FirstClassAutoSalvage);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath("/17511"), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("{3.EX.'17511'}", fixture.Quickbase.Queries.Last().Where);
        }

        // Sad paths, kept separate.

        [Theory]
        [MemberData(nameof(SupplierRoutes), MemberType = typeof(SupplierScoping))]
        public async Task Another_supplier_is_not_found_and_quickbase_is_not_asked(string route)
        {
            var token = await fixture.SeedExternalSupplierUserAsync(17511, 9583);
            fixture.Quickbase.RespondWith(SupplierResponses.FirstClassAutoSalvage);
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath($"/4242{route}"), token);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("supplier_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }
    }

    public class Given_an_external_user_with_no_linked_suppliers(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task The_list_is_an_empty_page_without_a_quickbase_query()
        {
            var token = await fixture.SeedExternalSupplierUserAsync();
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(0, body.GetProperty("items").GetArrayLength());
            Assert.Equal(0, body.GetProperty("totalRecords").GetInt32());
            Assert.Equal("None", body.GetProperty("freshness").GetProperty("source").GetString());
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }

        [Fact]
        public async Task Any_supplier_is_not_found()
        {
            var token = await fixture.SeedExternalSupplierUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath("/17511"), token);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class Given_a_user_without_a_supplier_view_permission(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task A_dropdown_returns_four_hundred_three()
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers/payment-terms", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("missing_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }
}
