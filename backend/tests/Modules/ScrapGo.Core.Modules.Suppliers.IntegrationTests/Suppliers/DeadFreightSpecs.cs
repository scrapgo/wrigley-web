// Dead freight is a two-option dropdown; Quickbase stores it as a checkbox (checked = Not Exempt).
using ScrapGo.Core.Modules.Suppliers.Application.Suppliers;
using ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class DeadFreightSpecs
{
    public class The_quickbase_mapping
    {
        [Theory]
        [InlineData(DeadFreight.Exempt, false)]
        [InlineData(DeadFreight.NotExempt, true)]
        public void Each_value_round_trips_through_the_checkbox(DeadFreight value, bool isChecked)
        {
            Assert.Equal(isChecked, QuickbaseDeadFreight.ToQuickbase(value));
            Assert.Equal(value, QuickbaseDeadFreight.FromQuickbase(isChecked));
        }

        [Fact]
        public void No_value_from_quickbase_is_null()
        {
            Assert.Null(QuickbaseDeadFreight.FromQuickbase(null));
        }

        [Theory]
        [InlineData(DeadFreight.Exempt, "Exempt")]
        [InlineData(DeadFreight.NotExempt, "Not Exempt")]
        public void Labels_are_the_dropdown_text(DeadFreight value, string label)
        {
            Assert.Equal(label, value.Label());
        }
    }

    public class The_options_endpoint(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Lists_the_dropdown_options_in_order_for_any_signed_in_user()
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers/dead-freight", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var options = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
                .Select(o => (o.GetProperty("value").GetString(), o.GetProperty("label").GetString()))
                .ToList();
            Assert.Equal([("Exempt", "Exempt"), ("Not Exempt", "Not Exempt")], options);
        }

        [Fact]
        public async Task An_anonymous_caller_gets_four_hundred_one()
        {
            var response = await fixture.Client.GetAsync("/api/suppliers/dead-freight");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
