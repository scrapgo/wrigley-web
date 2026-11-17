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

    /// <param name="loads">The supplier's delivered loads, newest Del Date first.</param>
    /// <param name="today">Today in the business time zone.</param>
    /// <param name="targetConsumerPrice">Suppliers field 346; used as the current offer when above 0.</param>
    public static SupplierDashboardStatsDto Compute(
        int supplierRecordId,
        IReadOnlyList<DeliveredLoad> loads,
        DateOnly today,
        decimal? targetConsumerPrice,
        DataFreshness freshness)
    {
        var yearStart = new DateOnly(today.Year, 1, 1);
        var cutoff = today.AddDays(-RecentDays);

        decimal paidYtd = 0, pendingAmount = 0;
        int pendingCount = 0, recent = 0, older = 0;

        foreach (var load in loads)
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
                if (delivered >= cutoff)
                {
                    recent++;
                }
                else
                {
                    older++;
                }
            }
        }

        var latest = loads.Count > 0 ? loads[0] : null;
        var (offer, source) = targetConsumerPrice > 0
            ? (targetConsumerPrice, SupplierTarget)
            : latest?.BuyPrice > 0
                ? (latest.BuyPrice, LastLoadBuyPrice)
                : ((decimal?)null, (string?)null);

        return new SupplierDashboardStatsDto(
            supplierRecordId,
            today,
            Math.Round(paidYtd, 2, MidpointRounding.AwayFromZero),
            Math.Round(pendingAmount, 2, MidpointRounding.AwayFromZero),
            pendingCount,
            recent,
            older,
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
