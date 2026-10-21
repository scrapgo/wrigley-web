// The ITS dispatch records linked to a supplier (Documents & Other): a list from
// the ITS Dispatch Records table, newest ship date first.
using ScrapGo.Core.Modules.Suppliers.Application.Suppliers;
using ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class DispatchRecordsSpecs
{
    private const string Path = "/17511/linked-its-dispatch-records";

    public class Given_a_supplier_reader(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Sends_the_dispatch_query_and_returns_the_records_newest_ship_date_first()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.DispatchRecords);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var query = fixture.Quickbase.Queries.Last();
            Assert.Equal("bspsb9kpm", query.TableId);
            Assert.Equal([3, 6, 241, 432, 433, 28, 508, 378, 14, 15, 8], query.Select);
            Assert.Equal("{366.EX.'17511'}", query.Where);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(17511, body.GetProperty("supplierRecordId").GetInt32());
            var records = body.GetProperty("dispatchRecords").EnumerateArray().ToList();
            Assert.Equal([9007, 9004, 9001], records.Select(r => r.GetProperty("recordId").GetInt32()));

            var delivered = records.Last();
            Assert.Equal("L-1001", delivered.GetProperty("loadNumber").GetString());
            Assert.Equal("Progress Rail - Atlanta, GA", delivered.GetProperty("consumer").GetString());
            Assert.Equal("Atlanta", delivered.GetProperty("consumerCity").GetString());
            Assert.Equal("GA", delivered.GetProperty("consumerState").GetString());
            Assert.Equal("ABC Trucking / Joe", delivered.GetProperty("carrierDriver").GetString());
            Assert.Equal("#1 HMS", delivered.GetProperty("material").GetString());
            Assert.Equal(42000.5m, delivered.GetProperty("weight").GetDecimal());
            Assert.Equal("2026-09-01", delivered.GetProperty("shipDate").GetString());
            Assert.Equal("2026-09-03", delivered.GetProperty("deliveryDate").GetString());
            Assert.Equal("Delivered", delivered.GetProperty("status").GetString());

            var onRoute = records.First();
            Assert.Equal("Nucor - FL", onRoute.GetProperty("consumer").GetString());
            Assert.Equal("On Route", onRoute.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, onRoute.GetProperty("deliveryDate").ValueKind);
            Assert.Equal(JsonValueKind.Null, onRoute.GetProperty("carrierDriver").ValueKind);

            // No consumer name: just the place. Unknown status text: null.
            var middle = records[1];
            Assert.Equal("Tampa, FL", middle.GetProperty("consumer").GetString());
            Assert.Equal(JsonValueKind.Null, middle.GetProperty("status").ValueKind);
        }

        [Fact]
        public async Task A_supplier_without_dispatch_records_returns_an_empty_list()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.Empty);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dispatchRecords").GetArrayLength());
        }

        [Fact]
        public async Task The_status_options_endpoint_lists_every_status_in_order()
        {
            var token = await fixture.SeedExternalSupplierUserAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Get, "/api/suppliers/linked-its-dispatch-records/statuses", token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var values = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
                .Select(o => o.GetProperty("value").GetString())
                .ToList();
            Assert.Equal(
                ["Pending", "Delivered", "Open", "On Route", "Completed", "Dispatched", "Refused", "Invoiced", "Test",
                 "Test2", "Loading", "Covered", "In Yard", "Unloading", "Roll Over"],
                values);
        }
    }

    public class Given_a_caller_without_supplier_access(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task A_user_without_a_supplier_view_permission_gets_four_hundred_three_and_no_query_runs()
        {
            var token = fixture.CreateToken((await fixture.SeedUserAsync()).Uid);
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }
    }

    public class The_consumer_format
    {
        [Theory]
        [InlineData("Progress Rail", "Atlanta", "GA", "Progress Rail - Atlanta, GA")]
        [InlineData("Progress Rail", null, "GA", "Progress Rail - GA")]
        [InlineData("Progress Rail", "Atlanta", null, "Progress Rail - Atlanta")]
        [InlineData("Progress Rail", null, null, "Progress Rail")]
        [InlineData(null, "Atlanta", "GA", "Atlanta, GA")]
        [InlineData(null, null, null, null)]
        public void Is_consumer_dash_city_comma_state_with_empty_parts_left_out(string? consumer, string? city, string? state, string? expected)
        {
            Assert.Equal(expected, DispatchRecordsTable.FormatConsumer(consumer, city, state));
        }

        [Fact]
        public void Every_status_round_trips_through_its_quickbase_text()
        {
            foreach (var status in Enum.GetValues<DispatchStatus>())
            {
                var text = QuickbaseDropdowns.ToQuickbase(DispatchStatuses.Catalog, status);
                Assert.True(QuickbaseDropdowns.TryFromQuickbase(DispatchStatuses.Catalog, text, out var parsed));
                Assert.Equal(status, parsed);
            }
        }
    }
}
