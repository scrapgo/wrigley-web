// The ITS dispatch records linked to a supplier (Documents & Other): a list from
// the ITS Dispatch Records table, newest ship date first.
using Microsoft.Extensions.DependencyInjection;
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
            Assert.Equal(
                [3, 6, 241, 432, 433, 28, 508, 378, 14, 15, 8,
                 424, 465, 502, 21, 622, 430, 431, 41, 32, 496, 497, 466, 517, 512, 386, 572, 398, 555, 607, 570, 593,
                 379, 559, 556, 579, 580, 563, 445],
                query.Select);
            Assert.DoesNotContain(22, query.Select!);
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

        [Theory]
        [InlineData("?status=Delivered", "{366.EX.'17511'}AND{8.EX.'Delivered'}")]
        [InlineData("?status=On%20Route&status=delivered", "{366.EX.'17511'}AND({8.EX.'On Route'}OR{8.EX.'Delivered'})")]
        [InlineData("", "{366.EX.'17511'}")]
        public async Task A_status_filter_is_added_to_the_where_clause(string query, string where)
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.DispatchRecords);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path + query), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(where, fixture.Quickbase.Queries.Last().Where);
        }

        [Fact]
        public async Task An_unknown_status_is_four_hundred_and_no_query_runs()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path + "?status=Lost"), token);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_status", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }

        [Fact]
        public async Task The_status_filter_options_are_every_status_but_the_test_ones()
        {
            var token = await fixture.SeedExternalSupplierUserAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Get, "/api/suppliers/linked-its-dispatch-records/status-filters", token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var values = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
                .Select(o => o.GetProperty("value").GetString())
                .ToList();
            Assert.Equal(
                ["Pending", "Delivered", "Open", "On Route", "Completed", "Dispatched", "Refused", "Invoiced",
                 "Loading", "Covered", "In Yard", "Unloading", "Roll Over"],
                values);
        }

        [Fact]
        public async Task Returns_every_load_form_field_of_load_54727()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.Load54727);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var load = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dispatchRecords")[0];
            Assert.Equal(51585, load.GetProperty("recordId").GetInt32());
            Assert.Equal("AMERICAN LOGISTICS, INC.", load.GetProperty("carrierDriver").GetString());
            Assert.Equal("1st Class Auto Salvage", load.GetProperty("supplier").GetString());
            Assert.Equal("https://scrapgo-9114.quickbase.com/db/bqrcgnatz?a=dr&rid=17511", load.GetProperty("goToSupplierUrl").GetString());
            Assert.Equal("1100131937", load.GetProperty("scaleTicketNumber").GetString());
            Assert.Equal("94004", load.GetProperty("shipperPoNumbers").GetString());
            Assert.Equal("1300", load.GetProperty("pickUpTime").GetString());
            Assert.Equal("Apopka", load.GetProperty("originationCity").GetString());
            Assert.Equal("FL", load.GetProperty("originationState").GetString());
            Assert.Equal("Flatbed", load.GetProperty("equipmentType").GetString());
            Assert.Equal(1350.00m, load.GetProperty("carrierFee").GetDecimal());
            Assert.Equal(68120m, load.GetProperty("totalGrossWt").GetDecimal());
            Assert.Equal(30080m, load.GetProperty("totalTareWt").GetDecimal());
            Assert.Equal(38040m, load.GetProperty("totalNetWt").GetDecimal());
            Assert.Equal(277.00m, load.GetProperty("salePrice").GetDecimal());
            Assert.Equal(200.00m, load.GetProperty("buyPrice").GetDecimal());
            Assert.Equal(3804.00m, load.GetProperty("paymentAmount").GetDecimal());
            Assert.Equal("2026-10-16", load.GetProperty("paymentDate").GetString());
            Assert.Equal(114.54m, load.GetProperty("grossProfit").GetDecimal());
            Assert.Equal(145.04m, load.GetProperty("totalCommissions").GetDecimal());
            Assert.Equal(7.44m, load.GetProperty("totalFreightCommissionExpense").GetDecimal());
            Assert.Equal(-182.97m, load.GetProperty("deadFreight").GetDecimal());
            Assert.Equal(JsonValueKind.Null, load.GetProperty("payDateOverride").ValueKind);
            Assert.Equal("Paid", load.GetProperty("paymentStatus").GetString());
            Assert.Equal(16.98214285714286m, load.GetProperty("weightInGt").GetDecimal());
            Assert.True(load.GetProperty("deadFreightRequirements").GetBoolean());
            Assert.Equal(-182.97m, load.GetProperty("totalDeadFreightViaCietrade").GetDecimal());
            Assert.Equal(JsonValueKind.Null, load.GetProperty("deadFreightChargeExceptions").ValueKind);
            Assert.False(load.GetProperty("supplierIsDeadFreightExempt").GetBoolean());
            Assert.Equal(44000m, load.GetProperty("minimumWeight").GetDecimal());
            Assert.Equal(JsonValueKind.Null, load.GetProperty("totalGrossSalesRevenue").ValueKind);
        }

        [Fact]
        public async Task Load_form_fields_missing_from_the_response_are_null()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.DispatchRecords);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            var load = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dispatchRecords")[0];
            foreach (var name in new[] { "supplier", "scaleTicketNumber", "carrierFee", "paymentDate", "deadFreightRequirements", "supplierIsDeadFreightExempt" })
            {
                Assert.Equal(JsonValueKind.Null, load.GetProperty(name).ValueKind);
            }
        }

        [Fact]
        public async Task Each_record_has_a_payment_status_from_its_status_and_payment_date()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            var later = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3).ToString("yyyy-MM-dd");
            fixture.Quickbase.RespondWith($$"""
                {
                  "data": [
                    { "3": { "value": 1 }, "8": { "value": "Delivered" }, "386": { "value": 3804.0 }, "572": { "value": "2026-01-02" } },
                    { "3": { "value": 2 }, "8": { "value": "Delivered" }, "386": { "value": 100.0 }, "572": { "value": "{{later}}" } },
                    { "3": { "value": 3 }, "8": { "value": "Delivered" }, "386": { "value": "" }, "572": { "value": "" } },
                    { "3": { "value": 4 }, "8": { "value": "Pending" }, "386": { "value": 3804.0 }, "572": { "value": "2026-01-02" } },
                    { "3": { "value": 5 }, "8": { "value": "Lost in transit" } }
                  ],
                  "fields": [],
                  "metadata": { "numRecords": 5, "skip": 0, "totalRecords": 5 }
                }
                """);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var statuses = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dispatchRecords")
                .EnumerateArray()
                .OrderBy(r => r.GetProperty("recordId").GetInt32())
                .Select(r => r.GetProperty("paymentStatus").GetString());
            Assert.Equal(["Paid", "Scheduled", "Unscheduled", "NotApplicable", "NotApplicable"], statuses);
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

        [Theory]
        [InlineData("/api/suppliers/{recordId}/linked-its-dispatch-records")]
        [InlineData("/api/suppliers/{recordId}/dashboard-stats")]
        public void Swagger_offers_the_status_filter_options_as_a_pick_list(string path)
        {
            var document = fixture.Services.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>().GetSwagger("v1");

            var status = document.Paths[path].Operations![HttpMethod.Get].Parameters!.Single(p => p.Name == "status");
            Assert.Equal("array", status.Schema!.Type.ToString()!.ToLowerInvariant());
            Assert.Equal(
                DispatchStatuses.FilterOptions.Select(option => option.Label),
                status.Schema.Items!.Enum!.Select(value => value!.GetValue<string>()));
            Assert.Equal(13, status.Schema.Items.Enum!.Count);
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
