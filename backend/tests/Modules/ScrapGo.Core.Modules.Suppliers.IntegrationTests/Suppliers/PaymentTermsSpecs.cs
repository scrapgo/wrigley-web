// Payment terms are a fixed dropdown list; each label is the Quickbase text.
using ScrapGo.Core.Modules.Suppliers.Application.Suppliers;
using ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class PaymentTermsSpecs
{
    public class The_quickbase_mapping
    {
        [Theory]
        [InlineData(PaymentTerms.Net5, "Net 5")]
        [InlineData(PaymentTerms.Net10, "Net 10")]
        [InlineData(PaymentTerms.Net30, "Net 30")]
        [InlineData(PaymentTerms.TuesdayThursday, "Tuesday/Thursday")]
        [InlineData(PaymentTerms.MlNorwood, "ML Norwood")]
        public void Each_value_round_trips_through_its_quickbase_text(PaymentTerms terms, string quickbaseText)
        {
            Assert.Equal(quickbaseText, QuickbasePaymentTerms.ToQuickbase(terms));
            Assert.True(QuickbasePaymentTerms.TryFromQuickbase(quickbaseText, out var parsed));
            Assert.Equal(terms, parsed);
        }

        [Theory]
        [InlineData(" net  5 ", PaymentTerms.Net5)]
        [InlineData("TUESDAY/THURSDAY", PaymentTerms.TuesdayThursday)]
        [InlineData("ml norwood", PaymentTerms.MlNorwood)]
        public void Reading_ignores_case_and_extra_spaces(string quickbaseText, PaymentTerms expected)
        {
            Assert.True(QuickbasePaymentTerms.TryFromQuickbase(quickbaseText, out var parsed));
            Assert.Equal(expected, parsed);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Net 15")]
        [InlineData("Net5")]
        public void Empty_or_unknown_text_is_not_a_value(string? quickbaseText)
        {
            Assert.False(QuickbasePaymentTerms.TryFromQuickbase(quickbaseText, out _));
        }

        [Fact]
        public void Every_enum_value_has_a_label()
        {
            Assert.Equal(Enum.GetValues<PaymentTerms>().Length, PaymentTermsCatalog.Options.Count);
            Assert.All(Enum.GetValues<PaymentTerms>(), terms => Assert.False(string.IsNullOrWhiteSpace(terms.Label())));
        }
    }

    public class The_options_endpoint(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Lists_the_dropdown_options_in_order_for_any_signed_in_user()
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers/payment-terms", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var options = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
                .Select(o => (o.GetProperty("value").GetString(), o.GetProperty("label").GetString()))
                .ToList();
            Assert.Equal(
                [
                    ("Net 5", "Net 5"),
                    ("Net 10", "Net 10"),
                    ("Net 30", "Net 30"),
                    ("Tuesday/Thursday", "Tuesday/Thursday"),
                    ("ML Norwood", "ML Norwood"),
                ],
                options);
        }

        [Fact]
        public async Task An_anonymous_caller_gets_four_hundred_one()
        {
            var response = await fixture.Client.GetAsync("/api/suppliers/payment-terms");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
