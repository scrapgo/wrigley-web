// A supplier's in-stock items: a list from the In-Stock Items table, with six dropdowns.
using ScrapGo.Core.Modules.Suppliers.Application.Suppliers;
using ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class InStockItemsSpecs
{
    private const string Path = "/17511/in-stock-items";

    public class Given_a_supplier_reader(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Sends_the_in_stock_items_query_and_maps_every_field()
        {
            var (token, organizationId) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);
            fixture.Quickbase.RespondWith(SupplierResponses.InStockItems);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var query = fixture.Quickbase.Queries.Last();
            Assert.Equal("bscpxjrw3", query.TableId);
            Assert.Equal([3, 15, 16, 6, 7, 8, 9, 10, 13, 14, 18, 19, 20, 21, 22, 23, 26, 27, 28, 31], query.Select);
            Assert.Equal("{15.EX.'17511'}", query.Where);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(17511, body.GetProperty("supplierRecordId").GetInt32());
            var items = body.GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(2, items.Count);

            var item = items[0];
            Assert.Equal(501, item.GetProperty("recordId").GetInt32());
            Assert.Equal(17511, item.GetProperty("relatedSupplier").GetInt32());
            Assert.Equal("1st Class Auto Salvage", item.GetProperty("supplierAccounting").GetString());
            Assert.Equal("Aluminum Radiators", item.GetProperty("materialType").GetString());
            Assert.Equal(2.5m, item.GetProperty("weight").GetDecimal());
            Assert.Equal("NT", item.GetProperty("unitOfMeasure").GetString());
            Assert.Equal(5000m, item.GetProperty("weightInLbs").GetDecimal());
            Assert.Equal("Wrapped/Palletized", item.GetProperty("preparation").GetString());
            Assert.Equal("Continous", item.GetProperty("availability").GetString());
            Assert.Equal(1250.75m, item.GetProperty("targetOffer").GetDecimal());
            Assert.Equal("Van Trailer", item.GetProperty("truckType").GetString());
            Assert.Equal(40m, item.GetProperty("volume").GetDecimal());
            Assert.Equal(1m, item.GetProperty("trucksPerWeek").GetDecimal());
            Assert.Equal(900m, item.GetProperty("freightCost").GetDecimal());
            Assert.True(item.GetProperty("verified").GetBoolean());
            Assert.Equal("Clean", item.GetProperty("dirtyOrClean").GetString());
            Assert.Equal("Ready for pickup", item.GetProperty("notes").GetString());
            Assert.Equal("Gaylords", item.GetProperty("volumeUom").GetString());
            Assert.Equal(2, item.GetProperty("supplierInStockItemRecords").GetInt32());
            Assert.Equal("Offered", item.GetProperty("offerStatus").GetString());

            // Unknown dropdown text and empty fields come back as null.
            var sparse = items[1];
            Assert.Equal(JsonValueKind.Null, sparse.GetProperty("materialType").ValueKind);
            Assert.Equal(JsonValueKind.Null, sparse.GetProperty("unitOfMeasure").ValueKind);
            Assert.Equal(JsonValueKind.Null, sparse.GetProperty("notes").ValueKind);
            Assert.False(sparse.GetProperty("verified").GetBoolean());
        }

        [Fact]
        public async Task A_supplier_without_items_returns_an_empty_list()
        {
            var (token, organizationId) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);
            fixture.Quickbase.RespondWith(SupplierResponses.Empty);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").GetArrayLength());
        }

        [Fact]
        public async Task Is_platform_wide_for_platform_administrators()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.InStockItems);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers" + Path, fixture.CreateToken(uid));

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
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, Path), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }

        [Fact]
        public async Task An_organization_reader_gets_four_hundred_three_on_the_platform_route()
        {
            var (token, _) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers" + Path, token);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class The_dropdowns(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Theory]
        [InlineData("material-types", "Aluminum Radiators", "Prepared Steel", 14)]
        [InlineData("units-of-measure", "NT", "MT", 5)]
        [InlineData("preparations", "Bales", "Wrapped/Palletized", 10)]
        [InlineData("availabilities", "Continous", "One Time", 2)]
        [InlineData("truck-types", "Dump Trailer", "Flatbread Trailer", 6)]
        [InlineData("dirty-or-clean", "Dirty", "Clean", 2)]
        public async Task Each_options_endpoint_lists_its_exact_text_in_order(string route, string first, string last, int count)
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, $"/api/suppliers/in-stock-items/{route}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var options = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
                .Select(o => (o.GetProperty("value").GetString(), o.GetProperty("label").GetString()))
                .ToList();
            Assert.Equal(count, options.Count);
            Assert.Equal((first, first), options[0]);
            Assert.Equal((last, last), options[^1]);
        }

        [Fact]
        public void Every_value_round_trips_through_its_quickbase_text()
        {
            AssertRoundTrips(InStockItemDropdowns.MaterialTypes);
            AssertRoundTrips(InStockItemDropdowns.UnitsOfMeasure);
            AssertRoundTrips(InStockItemDropdowns.Preparations);
            AssertRoundTrips(InStockItemDropdowns.Availabilities);
            AssertRoundTrips(InStockItemDropdowns.TruckTypes);
            AssertRoundTrips(InStockItemDropdowns.DirtyOrClean);
        }

        private static void AssertRoundTrips<TEnum>(DropdownCatalog<TEnum> catalog)
            where TEnum : struct, Enum
        {
            foreach (var value in Enum.GetValues<TEnum>())
            {
                var text = QuickbaseDropdowns.ToQuickbase(catalog, value);
                Assert.Equal(JsonSerializer.Serialize(text), JsonSerializer.Serialize(value));
                Assert.True(QuickbaseDropdowns.TryFromQuickbase(catalog, text, out var parsed));
                Assert.Equal(value, parsed);
            }
        }
    }
}
