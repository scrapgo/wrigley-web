// Suppliers come from Quickbase (bqrcgnatz) through the shared query service,
// for holders of suppliers.view_all or suppliers.view_own (B13 scopes the latter).
using ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class ReadingSuppliers
{
    public class Given_a_supplier_reader(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Get_by_record_id_sends_the_detail_query_and_maps_every_field()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.FirstClassAutoSalvage);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath("/17511"), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var query = fixture.Quickbase.Queries.Last();
            Assert.Equal("bqrcgnatz", query.TableId);
            Assert.Equal([3, 8, 9, 10, 11, 64, 12, 355, 123, 320, 301, 74, 28, 133, 116, 346, 129, 214, 321], query.Select);
            Assert.Equal("{3.EX.'17511'}", query.Where);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var supplier = body.GetProperty("supplier");
            Assert.Equal(17511, supplier.GetProperty("recordId").GetInt32());
            Assert.Equal("1st Class Auto Salvage", supplier.GetProperty("account").GetString());
            Assert.Equal("2350 Vulcan Rd", supplier.GetProperty("streetAddress").GetString());
            Assert.Equal("Apopka", supplier.GetProperty("city").GetString());
            Assert.Equal("FL", supplier.GetProperty("state").GetString());
            Assert.Equal("United States", supplier.GetProperty("country").GetString());
            Assert.Equal("32703", supplier.GetProperty("zipCode").GetString());
            Assert.Equal("(321) 356-4622", supplier.GetProperty("mainContactPhone").GetString());
            Assert.Equal("David Esteves ", Assert.Single(supplier.GetProperty("mainContactNames").EnumerateArray()).GetString());
            Assert.Equal("Net 5", supplier.GetProperty("paymentTerms").GetString());
            Assert.Equal("pipoe720@hotmail.com", supplier.GetProperty("mainEmail").GetString());
            Assert.Equal("john@scrapgo.com", supplier.GetProperty("leadAssignedTo").GetProperty("email").GetString());
            Assert.Equal(67, supplier.GetProperty("relevantConsumerDistances").GetInt32());
            Assert.Equal(JsonValueKind.Null, supplier.GetProperty("totalActivities").ValueKind);
            Assert.Equal(JsonValueKind.Null, supplier.GetProperty("inStockItemRecords").ValueKind);
            Assert.Equal(257m, supplier.GetProperty("targetConsumerPrice").GetDecimal());
            Assert.Equal(2, supplier.GetProperty("deliveredLast90Days").GetInt32());
            Assert.Equal(2, supplier.GetProperty("deliveredBefore90Days").GetInt32());
            Assert.Equal("Not Exempt", supplier.GetProperty("deadFreight").GetString());
            Assert.Equal("Quickbase", body.GetProperty("freshness").GetProperty("source").GetString());
        }

        // Quickbase text that isn't one of the dropdown's values comes back empty.
        [Fact]
        public async Task Unknown_payment_terms_come_back_as_null()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.UnknownPaymentTerms);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath("/9583"), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var supplier = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("supplier");
            Assert.Equal(JsonValueKind.Null, supplier.GetProperty("paymentTerms").ValueKind);
        }

        [Fact]
        public async Task An_unchecked_dead_freight_box_is_exempt()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.DeadFreightUnchecked);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath("/9583"), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var supplier = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("supplier");
            Assert.Equal("Exempt", supplier.GetProperty("deadFreight").GetString());
        }

        [Fact]
        public async Task An_unknown_record_returns_four_hundred_four()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.Empty);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath("/99999999"), token);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("supplier_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task The_list_sends_the_name_query_sorted_by_name_and_pages()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.NameList);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var query = fixture.Quickbase.Queries.Last();
            Assert.Equal("bqrcgnatz", query.TableId);
            Assert.Equal([3, 8], query.Select);
            Assert.Equal("{8.XEX.''}", query.Where);
            var sort = Assert.Single(query.SortBy!);
            Assert.Equal(8, sort.FieldId);
            Assert.Equal(QuickbaseSortOrder.Ascending, sort.Order);
            Assert.Equal(0, query.Skip);
            Assert.Equal(SupplierService.DefaultTop, query.Top);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(35420, body.GetProperty("totalRecords").GetInt32());
            var items = body.GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(4, items.Count);
            Assert.Equal(8701, items[0].GetProperty("recordId").GetInt32());
            Assert.Equal(" C & M Car Crushing Inc", items[0].GetProperty("account").GetString());
            Assert.Equal("\"Cash For Junk Cars\" Michael's Auto & Towing", items[1].GetProperty("account").GetString());
        }

        [Fact]
        public async Task Search_adds_an_escaped_contains_filter()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.NameList);

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath("?search=Michael%27s&skip=50&top=25"), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var query = fixture.Quickbase.Queries.Last();
            Assert.Equal("{8.XEX.''}AND{8.CT.'Michael\\'s'}", query.Where);
            Assert.Equal(50, query.Skip);
            Assert.Equal(25, query.Top);
        }

        [Theory]
        [InlineData("?top=0")]
        [InlineData("?top=1001")]
        [InlineData("?skip=-1")]
        public async Task Out_of_range_paging_returns_four_hundred(string queryString)
        {
            var token = await fixture.SeedSupplierReaderAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(queryString), token);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task A_quickbase_failure_returns_five_hundred_two()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.FailWith(503);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath("/17511"), token);

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.Equal("quickbase_unavailable", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_a_caller_without_supplier_access(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        // Denied before Quickbase is touched.
        [Fact]
        public async Task A_user_without_a_supplier_view_permission_gets_four_hundred_three_and_no_query_runs()
        {
            var (uid, _) = await fixture.SeedUserAsync();
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath("/17511"), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("missing_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }

        [Fact]
        public async Task An_anonymous_caller_gets_four_hundred_one()
        {
            var response = await fixture.Client.GetAsync(SuppliersSpecFixture.SuppliersPath());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    public class The_permission_contract
    {
        // Modules can't reference each other, so the string is pinned to Identity's catalog here.
        [Fact]
        public void The_supplier_view_permissions_match_the_catalog()
        {
            Assert.Equal(ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions.SuppliersViewAll, SupplierPermissions.ViewAll);
            Assert.Equal(ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions.SuppliersViewOwn, SupplierPermissions.ViewOwn);
        }
    }
}
