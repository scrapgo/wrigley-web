namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// Works out the supplier Dashboard's stats from its delivered loads (Quickbase
/// returns rows, not sums). Pure: "today" and the supplier's target price are passed in.
/// </summary>
public static class SupplierDashboardStats
{
    public const int RecentDays = 90;

    public const string SupplierTarget = "supplierTarget";

    public const string LastLoadBuyPrice = "lastLoadBuyPrice";

    /// <param name="loads">The supplier's loads, newest Del Date first.</param>
    /// <param name="statuses">The statuses that count (e.g. Delivered); other loads are ignored by every stat.</param>
    /// <param name="today">Today in the business time zone.</param>
    /// <param name="targetConsumerPrice">Suppliers field 346; used as the current offer when above 0.</param>
    /// <param name="onBlankAmount">Called once for each counted load without a Payment Amount (386; added as 0).</param>
    public static SupplierDashboardStatsDto Compute(
        int supplierRecordId,
        IReadOnlyList<DeliveredLoad> loads,
        IReadOnlyCollection<string> statuses,
        DateOnly today,
        decimal? targetConsumerPrice,
        DataFreshness freshness,
        Action<DeliveredLoad>? onBlankAmount = null)
    {
        var cutoff = today.AddDays(-RecentDays);
        var counted = loads.Where(load => load.Status is { } status && statuses.Contains(status, StringComparer.OrdinalIgnoreCase)).ToList();

        // Paid YTD and pending: the same rule as every other payment total.
        var payments = SupplierPaymentService.Summarize(counted, statuses, today, onBlankAmount);

        decimal recentAmount = 0, olderAmount = 0;
        int recent = 0, older = 0;

        foreach (var load in counted)
        {
            // Each bucket's count and amount describe the same loads.
            if (load.DeliveryDate is not { } delivered)
            {
                continue;
            }

            var amount = load.PaymentAmount ?? 0;
            if (delivered >= cutoff)
            {
                recent++;
                recentAmount += amount;
            }
            else
            {
                older++;
                olderAmount += amount;
            }
        }

        var latest = counted.Count > 0 ? counted[0] : null;
        var (offer, source) = targetConsumerPrice > 0
            ? (targetConsumerPrice, SupplierTarget)
            : latest?.BuyPrice > 0
                ? (latest.BuyPrice, LastLoadBuyPrice)
                : ((decimal?)null, (string?)null);

        return new SupplierDashboardStatsDto(
            supplierRecordId,
            today,
            payments.TotalPaidYtd,
            payments.PaidYtdCount,
            payments.PendingPaymentAmount,
            payments.PendingPaymentCount,
            recent,
            older,
            SupplierPaymentService.Round(recentAmount),
            SupplierPaymentService.Round(olderAmount),
            offer,
            source,
            latest?.DeliveryDate,
            latest?.LoadNumber,
            freshness);
    }

    /// <summary>Shipper PO numbers shared by more than one delivered load (split loads can, legitimately).</summary>
    public static IReadOnlyList<string> SharedShipperPos(IReadOnlyList<DeliveredLoad> loads) =>
    [
        .. loads
            .Where(load => load.ShipperPo is not null)
            .GroupBy(load => load.ShipperPo!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key),
    ];
}
