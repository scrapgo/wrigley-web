// A supplier's in-stock items for Materials Pricing: a list from the In-Stock Items table, with four dropdowns.
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
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.InStockItems);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var query = fixture.Quickbase.Queries.Last();
            Assert.Equal("bscpxjrw3", query.TableId);
            Assert.Equal([3, 15, 6, 10, 10, 7, 8, 14, 18, 20, 19], query.Select);
            Assert.Equal("{15.EX.'17511'}", query.Where);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(17511, body.GetProperty("supplierRecordId").GetInt32());
            var items = body.GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(2, items.Count);

            var item = items[0];
            Assert.Equal(501, item.GetProperty("recordId").GetInt32());
            Assert.Equal(17511, item.GetProperty("relatedSupplier").GetInt32());
            Assert.Equal("Aluminum Radiators", item.GetProperty("materialType").GetString());
            Assert.Equal("Loose or Baled", item.GetProperty("packaging").GetString());
            Assert.Equal("Loose or Baled", item.GetProperty("prep").GetString());
            Assert.Equal(2.5m, item.GetProperty("weight").GetDecimal());
            Assert.Equal("NT", item.GetProperty("unitOfMeasure").GetString());
            Assert.Equal(1250.75m, item.GetProperty("targetOffer").GetDecimal());
            Assert.Equal("Van Trailer", item.GetProperty("truckType").GetString());
            Assert.Equal(1.5m, item.GetProperty("trucksPerWeek").GetDecimal());
            Assert.Equal(40m, item.GetProperty("volume").GetDecimal());

            // The removed fields are no longer returned.
            foreach (var removed in new[] { "supplierAccounting", "weightInLbs", "availability", "freightCost", "verified", "dirtyOrClean", "notes", "volumeUom", "supplierInStockItemRecords", "offerStatus" })
            {
                Assert.False(item.TryGetProperty(removed, out _), removed);
            }

            // Unknown dropdown text (including the old spelling "Loose or Bailed") comes back as null.
            var sparse = items[1];
            Assert.Equal(JsonValueKind.Null, sparse.GetProperty("materialType").ValueKind);
            Assert.Equal(JsonValueKind.Null, sparse.GetProperty("unitOfMeasure").ValueKind);
            Assert.Equal(JsonValueKind.Null, sparse.GetProperty("packaging").ValueKind);
            Assert.Equal(JsonValueKind.Null, sparse.GetProperty("prep").ValueKind);
        }

        [Fact]
        public async Task A_supplier_without_items_returns_an_empty_list()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.Empty);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").GetArrayLength());
        }
    }

    public class Given_a_caller_without_supplier_access(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task A_user_who_is_not_a_platform_administrator_gets_four_hundred_three_and_no_query_runs()
        {
            var (uid, _) = await fixture.SeedUserAsync();
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }
    }

    public class The_dropdowns(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Theory]
        [InlineData("material-types", "Aluminum Radiators", "Prepared Steel", 14)]
        [InlineData("units-of-measure", "NT", "MT", 5)]
        [InlineData("preparations", "Wrapped / Palletized", "Unprepared", 10)]
        [InlineData("truck-types", "Dump Trailer", "Flatbread Trailer", 6)]
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
        public void Preparation_uses_loose_or_baled()
        {
            Assert.Equal(
                ["Wrapped / Palletized", "Bales", "Boxed", "Crushed / Flattened", "Logged", "Loose", "Loose or Baled",
                 "Loose or Logged", "Loose or Palletized", "Unprepared"],
                InStockItemDropdowns.Preparations.Options.Select(o => o.Label));
            Assert.Contains(InStockItemDropdowns.Preparations.Options, o => o.Label == "Loose or Baled");
            Assert.DoesNotContain(InStockItemDropdowns.Preparations.Options, o => o.Label == "Loose or Bailed");
        }

        [Theory]
        [InlineData("availabilities")]
        [InlineData("dirty-or-clean")]
        public async Task The_removed_dropdowns_no_longer_have_options_endpoints(string route)
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, $"/api/suppliers/in-stock-items/{route}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public void Every_value_round_trips_through_its_quickbase_text()
        {
            AssertRoundTrips(InStockItemDropdowns.MaterialTypes);
            AssertRoundTrips(InStockItemDropdowns.UnitsOfMeasure);
            AssertRoundTrips(InStockItemDropdowns.Preparations);
            AssertRoundTrips(InStockItemDropdowns.TruckTypes);
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
