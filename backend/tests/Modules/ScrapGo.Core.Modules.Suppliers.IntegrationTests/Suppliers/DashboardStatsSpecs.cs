// The supplier Dashboard's stats: worked out in the API from the supplier's
// delivered ITS Dispatch loads (Quickbase returns rows, not sums).
using ScrapGo.Core.Modules.Suppliers.Application.Abstractions;
using ScrapGo.Core.Modules.Suppliers.Application.Suppliers;
using ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class DashboardStatsSpecs
{
    private const string Path = "/17511/dashboard-stats";

    private const string SuppliersTableId = "bqrcgnatz";

    private static readonly DataFreshness Fresh = new("Quickbase", DateTimeOffset.UnixEpoch);

    public class The_dashboard_calculation
    {
        private static readonly DateOnly Today = new(2026, 11, 17);

        private static DeliveredLoad Load(
            string? delivered = null,
            decimal? amount = null,
            string? paid = null,
            decimal? buyPrice = null,
            string? loadNumber = null,
            decimal? quote = null,
            string status = "Delivered") =>
            new(1, loadNumber, null, Parse(delivered), amount, Parse(paid), buyPrice, status, quote);

        private static DateOnly? Parse(string? date) => date is null ? null : DateOnly.Parse(date);

        private static SupplierDashboardStatsDto Compute(
            IReadOnlyList<DeliveredLoad> loads, decimal? target = null, Action<DeliveredLoad>? onBlankQuote = null) =>
            SupplierDashboardStats.Compute(17511, loads, ["Delivered"], Today, target, Fresh, onBlankQuote);

        [Fact]
        public void A_delivered_load_in_the_last_ninety_days_adds_its_quote_buy_price_total()
        {
            var stats = Compute([Load(delivered: "2026-10-07", quote: 3804.00m)]);

            Assert.Equal(1, stats.LoadsLast90Days);
            Assert.Equal(3804.00m, stats.DeliveredLast90DaysAmount);
        }

        [Fact]
        public void The_ninety_day_amount_includes_exactly_ninety_days_ago_and_excludes_one_day_earlier()
        {
            var stats = Compute([Load(delivered: "2026-08-19", quote: 100m), Load(delivered: "2026-08-18", quote: 40m)]);

            Assert.Equal(100m, stats.DeliveredLast90DaysAmount);
            Assert.Equal(40m, stats.DeliveredBefore90DaysAmount);
        }

        [Fact]
        public void A_pending_load_counts_in_no_stat()
        {
            var stats = Compute([Load(delivered: "2026-10-01", quote: 500m, amount: 500m, status: "Pending")]);

            Assert.Equal(0, stats.LoadsLast90Days);
            Assert.Equal(0m, stats.DeliveredLast90DaysAmount);
            Assert.Equal(0, stats.PendingPaymentCount);
        }

        [Fact]
        public void A_delivered_load_without_a_quote_buy_price_total_adds_zero_and_is_reported()
        {
            var reported = new List<DeliveredLoad>();
            var stats = Compute(
                [Load(delivered: "2026-10-07", quote: 3919.70m), Load(delivered: "2026-10-01", loadNumber: "54999")],
                onBlankQuote: reported.Add);

            Assert.Equal(2, stats.LoadsLast90Days);
            Assert.Equal(3919.70m, stats.DeliveredLast90DaysAmount);
            Assert.Equal(["54999"], reported.Select(load => load.LoadNumber));
        }

        [Fact]
        public void The_ninety_day_amounts_are_rounded_to_cents_and_zero_without_loads()
        {
            Assert.Equal(3.12m, Compute([Load(delivered: "2026-11-01", quote: 1.005m), Load(delivered: "2026-11-02", quote: 2.111m)]).DeliveredLast90DaysAmount);
            Assert.Equal(0m, Compute([]).DeliveredLast90DaysAmount);
            Assert.Equal(0m, Compute([]).DeliveredBefore90DaysAmount);
        }

        [Fact]
        public void A_load_paid_this_year_counts_in_total_paid_ytd()
        {
            // Load 54727: paid 2026-10-16, $3,804.
            var stats = Compute([Load(amount: 3804.00m, paid: "2026-10-16")]);

            Assert.Equal(3804.00m, stats.TotalPaidYtd);
            Assert.Equal(0, stats.PendingPaymentCount);
        }

        [Fact]
        public void A_load_paid_after_today_is_pending()
        {
            var stats = Compute([Load(amount: 100m, paid: "2026-11-20")]);

            Assert.Equal(100m, stats.PendingPaymentAmount);
            Assert.Equal(1, stats.PendingPaymentCount);
            Assert.Equal(0m, stats.TotalPaidYtd);
        }

        [Fact]
        public void A_load_without_a_payment_date_is_pending()
        {
            var stats = Compute([Load(amount: 250.50m)]);

            Assert.Equal(250.50m, stats.PendingPaymentAmount);
            Assert.Equal(1, stats.PendingPaymentCount);
        }

        [Fact]
        public void A_load_paid_last_year_counts_in_neither()
        {
            var stats = Compute([Load(amount: 999m, paid: "2025-12-30")]);

            Assert.Equal(0m, stats.TotalPaidYtd);
            Assert.Equal(0m, stats.PendingPaymentAmount);
            Assert.Equal(0, stats.PendingPaymentCount);
        }

        [Fact]
        public void A_load_paid_today_counts_as_paid()
        {
            Assert.Equal(10m, Compute([Load(amount: 10m, paid: "2026-11-17")]).TotalPaidYtd);
        }

        [Fact]
        public void A_load_delivered_exactly_ninety_days_ago_is_recent_and_one_day_earlier_is_not()
        {
            var stats = Compute([Load(delivered: "2026-08-19"), Load(delivered: "2026-08-18")]);

            Assert.Equal(1, stats.LoadsLast90Days);
            Assert.Equal(1, stats.DeliveredBefore90Days);
        }

        [Fact]
        public void No_loads_gives_zeros_and_no_offer()
        {
            var stats = Compute([]);

            Assert.Equal(0m, stats.TotalPaidYtd);
            Assert.Equal(0m, stats.PendingPaymentAmount);
            Assert.Equal(0, stats.LoadsLast90Days);
            Assert.Equal(0, stats.DeliveredBefore90Days);
            Assert.Null(stats.CurrentOffer);
            Assert.Null(stats.CurrentOfferSource);
            Assert.Null(stats.LastDeliveredDate);
            Assert.Null(stats.LastLoadNumber);
        }

        [Fact]
        public void Without_a_supplier_target_the_offer_is_the_latest_loads_buy_price()
        {
            var stats = Compute(
                [Load(delivered: "2026-10-07", buyPrice: 200m, loadNumber: "54727"), Load(delivered: "2026-09-01", buyPrice: 180m)],
                target: 0m);

            Assert.Equal(200m, stats.CurrentOffer);
            Assert.Equal("lastLoadBuyPrice", stats.CurrentOfferSource);
            Assert.Equal(new DateOnly(2026, 10, 7), stats.LastDeliveredDate);
            Assert.Equal("54727", stats.LastLoadNumber);
        }

        [Fact]
        public void A_supplier_target_above_zero_wins()
        {
            var stats = Compute([Load(buyPrice: 200m)], target: 257m);

            Assert.Equal(257m, stats.CurrentOffer);
            Assert.Equal("supplierTarget", stats.CurrentOfferSource);
        }

        [Fact]
        public void Sums_are_rounded_to_cents()
        {
            var stats = Compute([Load(amount: 1.005m, paid: "2026-01-02"), Load(amount: 2.111m, paid: "2026-01-03")]);

            Assert.Equal(3.12m, stats.TotalPaidYtd);
        }

        [Fact]
        public void Shared_shipper_pos_are_reported_not_dropped()
        {
            IReadOnlyList<DeliveredLoad> loads =
            [
                new(1, "A", "PO-1", null, 1m, null, null, "Delivered", null),
                new(2, "B", "po-1", null, 1m, null, null, "Delivered", null),
                new(3, "C", "PO-2", null, 1m, null, null, "Delivered", null),
            ];

            Assert.Equal(["PO-1"], SupplierDashboardStats.SharedShipperPos(loads));
            Assert.Equal(3, Compute(loads).PendingPaymentCount);
        }
    }

    public class The_status_filter
    {
        [Fact]
        public void Is_delivered_only_by_default()
        {
            Assert.Equal(["Delivered"], new SupplierDashboardOptions().EffectiveStatuses);
            Assert.Equal(
                "{366.EX.'17511'}AND{8.EX.'Delivered'}",
                DispatchRecordsTable.BySupplierWithStatus(17511, ["Delivered"]));
        }

        [Fact]
        public void Ors_several_statuses_and_escapes_quotes()
        {
            Assert.Equal(
                "{366.EX.'17511'}AND({8.EX.'Delivered'}OR{8.EX.'It\\'s done'})",
                DispatchRecordsTable.BySupplierWithStatus(17511, ["Delivered", "It's done"]));
        }
    }

    public class Given_a_supplier_reader(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Pages_through_the_delivered_loads_and_returns_the_stats()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            var today = TodayInChicago();
            var thisYear = new DateOnly(today.Year, 1, 1);

            // Two pages; the first comes back shorter than asked for.
            var page1 = Page(
                total: 3,
                Row(54727, "54727", today.AddDays(-10), 3804.00m, thisYear, 200m),
                Row(54700, "54700", today.AddDays(-100), 150m, null, 190m));
            var page2 = Page(total: 3, Row(54600, "54600", today.AddDays(-200), 75.25m, "", 180m));
            fixture.Quickbase.RespondWith(query => query.TableId switch
            {
                SuppliersTableId => SupplierWithTarget(0),
                _ => query.Skip == 0 ? page1 : page2,
            });
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var loadQueries = fixture.Quickbase.Queries.Skip(before).Where(q => q.TableId == "bspsb9kpm").ToList();
            Assert.Equal([0, 2], loadQueries.Select(q => q.Skip));
            var query = loadQueries[0];
            Assert.Equal([3, 366, 8, 6, 21, 15, 386, 572, 512, 514], query.Select);
            Assert.Equal("{366.EX.'17511'}AND{8.EX.'Delivered'}", query.Where);
            Assert.Equal([new QuickbaseSort(15, QuickbaseSortOrder.Descending)], query.SortBy);
            Assert.Equal(1000, query.Top);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(17511, body.GetProperty("relatedSupplierId").GetInt32());
            Assert.Equal(today.ToString("yyyy-MM-dd"), body.GetProperty("asOf").GetString());
            Assert.Equal(3804.00m, body.GetProperty("totalPaidYtd").GetDecimal());
            Assert.Equal(225.25m, body.GetProperty("pendingPaymentAmount").GetDecimal());
            Assert.Equal(2, body.GetProperty("pendingPaymentCount").GetInt32());
            Assert.Equal(1, body.GetProperty("loadsLast90Days").GetInt32());
            Assert.Equal(2, body.GetProperty("deliveredBefore90Days").GetInt32());
            Assert.Equal(3804.00m, body.GetProperty("deliveredLast90DaysAmount").GetDecimal());
            Assert.Equal(225.25m, body.GetProperty("deliveredBefore90DaysAmount").GetDecimal());
            Assert.Equal(200m, body.GetProperty("currentOffer").GetDecimal());
            Assert.Equal("lastLoadBuyPrice", body.GetProperty("currentOfferSource").GetString());
            Assert.Equal(today.AddDays(-10).ToString("yyyy-MM-dd"), body.GetProperty("lastDeliveredDate").GetString());
            Assert.Equal("54727", body.GetProperty("lastLoadNumber").GetString());
        }

        [Fact]
        public async Task A_supplier_without_loads_gets_zeros_and_nulls_not_four_hundred_four()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(query => query.TableId == SuppliersTableId ? SupplierWithTarget(0) : SupplierResponses.Empty);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(0m, body.GetProperty("totalPaidYtd").GetDecimal());
            Assert.Equal(0, body.GetProperty("loadsLast90Days").GetInt32());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("currentOffer").ValueKind);
            Assert.Equal(JsonValueKind.Null, body.GetProperty("currentOfferSource").ValueKind);
            Assert.Equal(JsonValueKind.Null, body.GetProperty("lastDeliveredDate").ValueKind);
        }

        [Fact]
        public async Task The_supplier_target_price_is_the_current_offer_when_set()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(query => query.TableId == SuppliersTableId
                ? SupplierResponses.FirstClassAutoSalvage
                : Page(total: 1, Row(1, "1", TodayInChicago(), 1m, null, 200m)));

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(257m, body.GetProperty("currentOffer").GetDecimal());
            Assert.Equal("supplierTarget", body.GetProperty("currentOfferSource").GetString());
        }

        [Theory]
        [InlineData("?status=Completed", "{366.EX.'17511'}AND{8.EX.'Completed'}")]
        [InlineData("?status=Delivered&status=Invoiced", "{366.EX.'17511'}AND({8.EX.'Delivered'}OR{8.EX.'Invoiced'})")]
        [InlineData("", "{366.EX.'17511'}AND{8.EX.'Delivered'}")]
        public async Task The_status_parameter_picks_the_loads_counted(string query, string where)
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(q => q.TableId == SuppliersTableId ? SupplierWithTarget(0) : SupplierResponses.Empty);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path + query), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(where, fixture.Quickbase.Queries.Last(q => q.TableId == "bspsb9kpm").Where);
        }

        [Fact]
        public async Task An_unknown_status_is_four_hundred_and_no_query_runs()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path + "?status=Test3"), token);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_status", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }

        [Fact]
        public async Task An_unknown_supplier_is_four_hundred_four_and_its_loads_are_not_queried()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.Empty);
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain(fixture.Quickbase.Queries.Skip(before), q => q.TableId == "bspsb9kpm");
        }

        [Fact]
        public async Task Quickbase_down_is_five_hundred_two()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.FailWith(503);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
    }

    public class Given_an_external_supplier_user(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task A_supplier_outside_their_scope_is_four_hundred_four_without_a_query()
        {
            var token = await fixture.SeedExternalSupplierUserAsync(9583);
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }
    }

    public class Given_a_caller_without_supplier_access(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Gets_four_hundred_three_and_no_query_runs()
        {
            var token = fixture.CreateToken((await fixture.SeedUserAsync()).Uid);
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }
    }

    private static DateOnly TodayInChicago() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Chicago")).DateTime);

    private static string SupplierWithTarget(decimal target) =>
        SupplierResponses.FirstClassAutoSalvage.Replace("\"346\": { \"value\": 257 }", $"\"346\": {{ \"value\": {target} }}", StringComparison.Ordinal);

    /// <param name="paid">A date, null for a JSON null, or "" for Quickbase's empty text.</param>
    private static string Row(int recordId, string loadNumber, DateOnly delivered, decimal amount, object? paid, decimal buyPrice)
    {
        var paidJson = paid switch
        {
            DateOnly date => $"\"{date:yyyy-MM-dd}\"",
            string text => $"\"{text}\"",
            _ => "null",
        };
        return $$"""
            {
              "3": { "value": {{recordId}} },
              "366": { "value": 17511 },
              "8": { "value": "Delivered" },
              "6": { "value": "{{loadNumber}}" },
              "21": { "value": "PO-{{recordId}}" },
              "15": { "value": "{{delivered:yyyy-MM-dd}}" },
              "386": { "value": {{amount}} },
              "572": { "value": {{paidJson}} },
              "512": { "value": {{buyPrice}} },
              "514": { "value": {{amount}} }
            }
            """;
    }

    private static string Page(int total, params string[] rows) =>
        $$"""{ "data": [ {{string.Join(",", rows)}} ], "fields": [], "metadata": { "numRecords": {{rows.Length}}, "totalRecords": {{total}} } }""";
}
