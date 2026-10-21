// A supplier's Target Pricing — Progress Rail: text, currency and numeric fields.
namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class TargetPricingProgressRailSpecs
{
    private const string Path = "/17511/target-pricing-progress-rail";

    public class Given_a_supplier_reader(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Sends_the_target_pricing_query_and_maps_every_field()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.TargetPricingProgressRail);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var query = fixture.Quickbase.Queries.Last();
            Assert.Equal("bqrcgnatz", query.TableId);
            Assert.Equal([3, 352, 342, 336, 337, 339, 340, 341, 346, 345, 347, 348, 349, 358, 363], query.Select);
            Assert.Equal("{3.EX.'17511'}", query.Where);

            var pricing = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("targetPricingProgressRail");
            Assert.Equal(17511, pricing.GetProperty("recordId").GetInt32());
            Assert.Equal("#1 HMS", pricing.GetProperty("targetMaterial").GetString());
            Assert.Equal(205.75m, pricing.GetProperty("targetBreakEven").GetDecimal());
            Assert.Equal(180m, pricing.GetProperty("targetOffer").GetDecimal());
            Assert.Equal("Net Ton", pricing.GetProperty("targetUom").GetString());
            Assert.Equal(2.5m, pricing.GetProperty("trucksPerWeek").GetDecimal());
            Assert.Equal(22m, pricing.GetProperty("targetFreightPerUom").GetDecimal());
            Assert.Equal(1100.5m, pricing.GetProperty("targetFreightCost").GetDecimal());
            Assert.Equal(257m, pricing.GetProperty("targetConsumerPrice").GetDecimal());
            Assert.Equal(257m, pricing.GetProperty("priceInNetTons").GetDecimal());
            Assert.Equal(0.1285m, pricing.GetProperty("priceInLbs").GetDecimal());
            Assert.Equal(12.85m, pricing.GetProperty("priceInCwt").GetDecimal());
            Assert.Equal(287.84m, pricing.GetProperty("priceInGrossTons").GetDecimal());
            // Empty text in Quickbase comes back as null.
            Assert.Equal(JsonValueKind.Null, pricing.GetProperty("targetPoNumber").ValueKind);
            Assert.Equal(-5m, pricing.GetProperty("priceChangeFromPrior").GetDecimal());
        }

        [Fact]
        public async Task An_unknown_record_returns_four_hundred_four()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.Empty);

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath("/99999999/target-pricing-progress-rail"), token);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("supplier_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_a_caller_without_supplier_access(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task A_user_without_a_supplier_view_permission_gets_four_hundred_three_and_no_query_runs()
        {
            var (uid, _) = await fixture.SeedUserAsync();
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }
    }
}
