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
    /// <param name="onBlankQuoteAmount">Called for each counted load with a Del Date but no Quote Buy Price Total (added as 0).</param>
    public static SupplierDashboardStatsDto Compute(
        int supplierRecordId,
        IReadOnlyList<DeliveredLoad> loads,
        IReadOnlyCollection<string> statuses,
        DateOnly today,
        decimal? targetConsumerPrice,
        DataFreshness freshness,
        Action<DeliveredLoad>? onBlankQuoteAmount = null)
    {
        var yearStart = new DateOnly(today.Year, 1, 1);
        var cutoff = today.AddDays(-RecentDays);
        var counted = loads.Where(load => load.Status is { } status && statuses.Contains(status, StringComparer.OrdinalIgnoreCase)).ToList();

        decimal paidYtd = 0, pendingAmount = 0, recentAmount = 0, olderAmount = 0;
        int pendingCount = 0, recent = 0, older = 0;

        foreach (var load in counted)
        {
            var amount = load.PaymentAmount ?? 0;
            if (load.PaymentDate is { } paid && paid >= yearStart && paid <= today)
            {
                paidYtd += amount;
            }
            else if (load.PaymentDate is null || load.PaymentDate > today)
            {
                pendingAmount += amount;
                pendingCount++;
            }

            // Paid before this year: in neither.
            if (load.DeliveryDate is { } delivered)
            {
                // Each bucket's count and amount describe the same loads.
                if (load.PaymentAmountQuote is null)
                {
                    onBlankQuoteAmount?.Invoke(load);
                }

                var quote = load.PaymentAmountQuote ?? 0;
                if (delivered >= cutoff)
                {
                    recent++;
                    recentAmount += quote;
                }
                else
                {
                    older++;
                    olderAmount += quote;
                }
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
            Round(paidYtd),
            Round(pendingAmount),
            pendingCount,
            recent,
            older,
            Round(recentAmount),
            Round(olderAmount),
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

    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
