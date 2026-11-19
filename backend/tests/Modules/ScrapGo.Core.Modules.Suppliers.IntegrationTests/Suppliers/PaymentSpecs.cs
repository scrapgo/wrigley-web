// Supplier payments from ITS Dispatch's scheduled values: Payment Amount (386) and
// Payment Date (572). There is no paid flag, so "paid" is a Payment Date on or before today.
using ScrapGo.Core.Modules.Suppliers.Application.Abstractions;
using ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class PaymentSpecs
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    private static readonly string[] Delivered = ["Delivered"];

    private static DateOnly? Parse(string? date) => date is null ? null : DateOnly.Parse(date);

    public class Classifying_a_payment
    {
        [Theory]
        [InlineData("Delivered", "2026-10-16", PaymentStatus.Paid)]
        [InlineData("Delivered", "2026-11-18", PaymentStatus.Paid)]
        [InlineData("Delivered", "2026-11-19", PaymentStatus.Scheduled)]
        [InlineData("Delivered", null, PaymentStatus.Unscheduled)]
        [InlineData("delivered", "2026-10-16", PaymentStatus.Paid)]
        [InlineData("Pending", "2026-10-16", PaymentStatus.NotApplicable)]
        [InlineData("Pending", "2026-11-19", PaymentStatus.NotApplicable)]
        [InlineData("Pending", null, PaymentStatus.NotApplicable)]
        [InlineData(null, "2026-10-16", PaymentStatus.NotApplicable)]
        public void Follows_the_status_and_the_payment_date(string? status, string? paymentDate, PaymentStatus expected) =>
            Assert.Equal(expected, SupplierPaymentService.ClassifyPayment(status, Parse(paymentDate), Today, Delivered));

        [Fact]
        public void Uses_the_counted_statuses_it_is_given() =>
            Assert.Equal(
                PaymentStatus.Paid,
                SupplierPaymentService.ClassifyPayment("Invoiced", Parse("2026-10-16"), Today, ["Delivered", "Invoiced"]));
    }

    public class Summarizing_payments
    {
        private static DeliveredLoad Load(decimal? amount, string? paid, string status = "Delivered", string? loadNumber = null) =>
            new(1, loadNumber, null, null, amount, Parse(paid), null, status, null);

        private static PaymentSummary Summarize(params DeliveredLoad[] loads) =>
            SupplierPaymentService.Summarize(loads, Delivered, Today);

        [Fact]
        public void A_load_paid_this_year_counts_in_total_paid_ytd()
        {
            var summary = Summarize(Load(3804.00m, "2026-10-16"), Load(3919.70m, "2026-01-01"));

            Assert.Equal(7723.70m, summary.TotalPaidYtd);
            Assert.Equal(2, summary.PaidYtdCount);
            Assert.Equal(0m, summary.PendingPaymentAmount);
            Assert.Equal(0, summary.PendingPaymentCount);
        }

        [Fact]
        public void A_load_paid_in_a_prior_year_is_in_neither_total()
        {
            var summary = Summarize(Load(999m, "2025-12-30"));

            Assert.Equal(new PaymentSummary(0m, 0, 0m, 0), summary);
        }

        [Fact]
        public void Scheduled_and_unscheduled_loads_are_pending()
        {
            var summary = Summarize(Load(100m, "2026-11-19"), Load(50m, null));

            Assert.Equal(150m, summary.PendingPaymentAmount);
            Assert.Equal(2, summary.PendingPaymentCount);
            Assert.Equal(0, summary.PaidYtdCount);
        }

        [Fact]
        public void A_load_without_an_amount_adds_zero_and_is_reported()
        {
            var reported = new List<DeliveredLoad>();
            var summary = SupplierPaymentService.Summarize(
                [Load(null, "2026-10-16", loadNumber: "54999"), Load(10m, "2026-10-16")], Delivered, Today, reported.Add);

            Assert.Equal(10m, summary.TotalPaidYtd);
            Assert.Equal(2, summary.PaidYtdCount);
            Assert.Equal(["54999"], reported.Select(load => load.LoadNumber));
        }

        [Fact]
        public void Not_applicable_loads_are_skipped()
        {
            var reported = new List<DeliveredLoad>();
            var summary = SupplierPaymentService.Summarize(
                [Load(500m, "2026-10-16", "Pending"), Load(null, null, "Pending")], Delivered, Today, reported.Add);

            Assert.Equal(new PaymentSummary(0m, 0, 0m, 0), summary);
            Assert.Empty(reported);
        }

        [Fact]
        public void Totals_are_rounded_to_cents()
        {
            var summary = Summarize(Load(1.005m, "2026-10-16"), Load(2.111m, "2026-10-17"), Load(0.125m, null));

            Assert.Equal(3.12m, summary.TotalPaidYtd);
            Assert.Equal(0.13m, summary.PendingPaymentAmount);
        }
    }
}
