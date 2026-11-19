namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <param name="TotalPaidYtd">Payment Amount (386) of Paid loads with a Payment Date from Jan 1 to today.</param>
/// <param name="PaidYtdCount">How many loads <paramref name="TotalPaidYtd"/> covers.</param>
/// <param name="PendingPaymentAmount">Payment Amount (386) of Scheduled and Unscheduled loads.</param>
/// <param name="PendingPaymentCount">How many loads <paramref name="PendingPaymentAmount"/> covers.</param>
public sealed record PaymentSummary(
    decimal TotalPaidYtd, int PaidYtdCount, decimal PendingPaymentAmount, int PendingPaymentCount);

/// <summary>
/// Supplier payments, derived from ITS Dispatch's scheduled values: Payment Amount (386)
/// and Payment Date (572). ITS Dispatch has no paid flag, check number or paid amount, so
/// "paid" is <see cref="ClassifyPayment"/>'s rule; replace it there when a real payment
/// source exists. Pure: "today" is passed in. Every payment total (the Dashboard's and the
/// Payments tab's) comes from <see cref="Summarize"/>.
/// </summary>
public static class SupplierPaymentService
{
    /// <param name="status">The load's status (field 8), as Quickbase text.</param>
    /// <param name="paymentDate">Payment Date (572), which already honors Pay Date Override (593).</param>
    /// <param name="today">Today in the business time zone.</param>
    /// <param name="countedStatuses">The statuses whose loads are paid (e.g. Delivered).</param>
    public static PaymentStatus ClassifyPayment(
        string? status, DateOnly? paymentDate, DateOnly today, IReadOnlyCollection<string> countedStatuses)
    {
        if (status is null || !countedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return PaymentStatus.NotApplicable;
        }

        return paymentDate switch
        {
            null => PaymentStatus.Unscheduled,
            { } date when date <= today => PaymentStatus.Paid,
            _ => PaymentStatus.Scheduled,
        };
    }

    /// <summary>
    /// Paid this year and pending totals. A load paid before Jan 1 counts in neither;
    /// a load whose status isn't counted is skipped.
    /// </summary>
    /// <param name="onBlankAmount">Called for each counted load without a Payment Amount (added as 0).</param>
    public static PaymentSummary Summarize(
        IReadOnlyList<DeliveredLoad> loads,
        IReadOnlyCollection<string> countedStatuses,
        DateOnly today,
        Action<DeliveredLoad>? onBlankAmount = null)
    {
        var yearStart = new DateOnly(today.Year, 1, 1);
        decimal paidYtd = 0, pending = 0;
        int paidCount = 0, pendingCount = 0;

        foreach (var load in loads)
        {
            var status = ClassifyPayment(load.Status, load.PaymentDate, today, countedStatuses);
            if (status == PaymentStatus.NotApplicable)
            {
                continue;
            }

            if (load.PaymentAmount is null)
            {
                onBlankAmount?.Invoke(load);
            }

            var amount = load.PaymentAmount ?? 0;
            if (status == PaymentStatus.Paid)
            {
                if (load.PaymentDate >= yearStart)
                {
                    paidYtd += amount;
                    paidCount++;
                }
            }
            else
            {
                pending += amount;
                pendingCount++;
            }
        }

        return new(Round(paidYtd), paidCount, Round(pending), pendingCount);
    }

    internal static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
