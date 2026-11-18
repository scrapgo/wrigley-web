namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>
/// The Quickbase ITS Dispatch Records table (<c>bspsb9kpm</c>): loads, linked
/// to a supplier by field 366.
/// </summary>
public static class DispatchRecordsTable
{
    public const string TableId = "bspsb9kpm";

    public const int RecordId = 3;
    public const int LoadNumber = 6;
    public const int Status = 8;
    public const int ShipDate = 14;
    public const int DeliveryDate = 15;
    public const int ShipperPoNumbers = 21;
    public const int CarrierDriver = 28;
    public const int Consumer = 241;

    /// <summary>The supplier's Record ID# (Suppliers table field 3).</summary>
    public const int RelatedSupplier = 366;
    public const int Weight = 378;

    /// <summary>Buy Price × Purchase Weight in NT, "Payment Amount" on the form (currency formula).</summary>
    public const int PaymentAmount = 386;
    public const int ConsumerCity = 432;
    public const int ConsumerState = 433;
    public const int Material = 508;

    /// <summary>"Query Fetch Buy Price from Quotes", "Buy Price" on the form (currency formula).</summary>
    public const int BuyPrice = 512;

    /// <summary>"Quote Buy Price Total" (currency): the load's dollar value at the quoted buy price.</summary>
    public const int PaymentAmountQuote = 514;

    /// <summary>Date formula that already honors Pay Date Override (593).</summary>
    public const int PaymentDate = 572;

    // The rest of the load form, returned by linked-its-dispatch-records.

    /// <summary>Field 424: Supplier (Shipper - Supplier - Account).</summary>
    public const int Supplier = 424;

    /// <summary>Field 465: Go To Supplier (URL).</summary>
    public const int GoToSupplier = 465;

    /// <summary>Field 502: Scale Ticket # on the form ("Receiving ID"; 501 is BOL #).</summary>
    public const int ScaleTicketNumber = 502;

    /// <summary>Field 622: Pick Up Time, as stored (e.g. "1300").</summary>
    public const int PickUpTime = 622;

    /// <summary>Field 430: Origination City (Shipper - Supplier - City).</summary>
    public const int OriginationCity = 430;

    /// <summary>Field 431: Origination State (Shipper - Supplier - State).</summary>
    public const int OriginationState = 431;

    /// <summary>Field 41: Equipment Type (multiple choice).</summary>
    public const int EquipmentType = 41;

    /// <summary>Field 32: Carrier Fee (currency).</summary>
    public const int CarrierFee = 32;

    /// <summary>Field 496: Total GrossWt.</summary>
    public const int TotalGrossWt = 496;

    /// <summary>Field 497: Total TareWt.</summary>
    public const int TotalTareWt = 497;

    /// <summary>Field 466: Total NetWt.</summary>
    public const int TotalNetWt = 466;

    /// <summary>Field 517: Sale Price ("Query Fetch Sale Price from Quotes").</summary>
    public const int SalePrice = 517;

    /// <summary>Field 398: Gross Profit (currency).</summary>
    public const int GrossProfit = 398;

    /// <summary>Field 555: Total Commissions (currency).</summary>
    public const int TotalCommissions = 555;

    /// <summary>Field 607: Total Freight Commission Expense (currency).</summary>
    public const int TotalFreightCommissionExpense = 607;

    /// <summary>Field 570: Dead Freight ("Dead Freight Lump Sum Distributed").</summary>
    public const int DeadFreight = 570;

    /// <summary>Field 593: Pay Date Override.</summary>
    public const int PayDateOverride = 593;

    /// <summary>Field 379: Weight in GT (unrounded).</summary>
    public const int WeightInGt = 379;

    /// <summary>Field 559: Dead Freight Requirements (text formula "True"/"False").</summary>
    public const int DeadFreightRequirements = 559;

    /// <summary>Field 556: Total Dead Freight via Cietrade (currency).</summary>
    public const int TotalDeadFreightViaCietrade = 556;

    /// <summary>Field 579: Dead Freight Charge Exceptions (multiple choice).</summary>
    public const int DeadFreightChargeExceptions = 579;

    /// <summary>Field 580: Supplier is Dead Freight Exempt (checkbox).</summary>
    public const int SupplierIsDeadFreightExempt = 580;

    /// <summary>Field 563: Minimum Weight ("Query Fetch Min Weight from Quotes").</summary>
    public const int MinimumWeight = 563;

    /// <summary>Field 445: Total Gross Sales Revenue (currency).</summary>
    public const int TotalGrossSalesRevenue = 445;

    /// <summary>The largest page Quickbase is asked for when every row is needed.</summary>
    public const int PageSize = 1000;

    /// <summary>
    /// Every other field on the ITS Dispatch load form, in the DTO's order.
    /// Declared before <see cref="Fields"/>, which spreads it: statics initialize in order.
    /// </summary>
    public static readonly IReadOnlyList<int> FormFields =
    [
        Supplier, GoToSupplier, ScaleTicketNumber, ShipperPoNumbers, PickUpTime, OriginationCity, OriginationState,
        EquipmentType, CarrierFee, TotalGrossWt, TotalTareWt, TotalNetWt, SalePrice, BuyPrice, PaymentAmount, PaymentDate,
        GrossProfit, TotalCommissions, TotalFreightCommissionExpense, DeadFreight, PayDateOverride, WeightInGt,
        DeadFreightRequirements, TotalDeadFreightViaCietrade, DeadFreightChargeExceptions, SupplierIsDeadFreightExempt,
        MinimumWeight, TotalGrossSalesRevenue,
    ];

    /// <summary>
    /// The portal's query, with field 6 (Load #) added so the load number can be
    /// shown (the original select omitted it), then the rest of the load form.
    /// </summary>
    /// <remarks>Field 22 ("Carrier") links to the Consumers table, not the carrier: 28 is the carrier.</remarks>
    public static readonly IReadOnlyList<int> Fields =
    [
        RecordId, LoadNumber, Consumer, ConsumerCity, ConsumerState, CarrierDriver, Material, Weight, ShipDate, DeliveryDate, Status,
        .. FormFields,
    ];

    /// <summary><c>{366.EX.'17511'}</c>: every dispatch record linked to this supplier.</summary>
    public static string BySupplier(int supplierRecordId) => $"{{{RelatedSupplier}.EX.'{supplierRecordId}'}}";

    /// <summary>The Dashboard stats' fields: one row per load, aggregated in the API.</summary>
    public static readonly IReadOnlyList<int> DashboardFields =
        [RecordId, RelatedSupplier, Status, LoadNumber, ShipperPoNumbers, DeliveryDate, PaymentAmount, PaymentDate, BuyPrice, PaymentAmountQuote];

    /// <summary>
    /// <c>{366.EX.'17511'}AND{8.EX.'Delivered'}</c>; several statuses are OR-ed
    /// in parentheses. Quotes in a status are escaped.
    /// </summary>
    public static string BySupplierWithStatus(int supplierRecordId, IReadOnlyList<string> statuses)
    {
        var parts = statuses.Select(status => $"{{{Status}.EX.'{status.Replace("'", "\\'", StringComparison.Ordinal)}'}}").ToList();
        var filter = parts.Count == 1 ? parts[0] : $"({string.Join("OR", parts)})";
        return $"{BySupplier(supplierRecordId)}AND{filter}";
    }

    /// <summary>
    /// "Consumer - City, State". Empty parts are left out: "Consumer - State",
    /// "Consumer", "City, State"; null when all three are empty.
    /// </summary>
    public static string? FormatConsumer(string? consumer, string? city, string? state)
    {
        var place = string.Join(", ", new[] { city, state }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
        var name = consumer?.Trim();

        return (string.IsNullOrEmpty(name), place.Length == 0) switch
        {
            (false, false) => $"{name} - {place}",
            (false, true) => name,
            (true, false) => place,
            _ => null,
        };
    }
}
