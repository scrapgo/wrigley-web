namespace ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

/// <summary>The Quickbase Suppliers table (<c>bqrcgnatz</c>) and the field ids this module reads.</summary>
/// <remarks>Field ids match the legacy integration's <c>SupplierFieldMap</c> (liverpool) where they overlap.</remarks>
public static class SuppliersTable
{
    public const string TableId = "bqrcgnatz";

    /// <summary>Quickbase's built-in Record ID#: the only unique key (names repeat).</summary>
    public const int RecordId = 3;
    public const int Account = 8;
    public const int StreetAddress = 9;
    public const int City = 10;
    public const int State = 11;
    public const int ZipCode = 12;
    public const int RelevantConsumerDistances = 28;
    public const int Country = 64;
    public const int LeadAssignedTo = 74;
    public const int TotalActivities = 116;
    public const int MainContactNames = 123;
    public const int DeliveredLast90Days = 129;
    public const int InStockItemRecords = 133;
    public const int DeliveredBefore90Days = 214;
    public const int CallBackDate = 181;
    public const int ProspectStatus = 192;
    public const int LastCallResult = 193;
    public const int ContactWithDecisionMakerMade = 197;
    public const int SupplierObjections = 236;
    public const int ObjectionExplained = 238;

    /// <summary>Call Notes. (The legacy integration's field map uses 136, which is wrong.)</summary>
    public const int CallNotes = 97;

    // Yard capabilities (checkboxes).
    public const int CrusherOnSite = 65;
    public const int LoadFlatbeds = 78;
    public const int LoadDumps = 182;
    public const int LoadVanTrailers = 183;
    public const int UsesOwnTrucks = 184;
    public const int BalerOnSite = 185;
    public const int LoggerOnSite = 186;
    public const int HasLoadWrap = 187;
    public const int CanExport = 204;
    public const int HasGaylordBoxes = 205;
    public const int MobileCrusher = 225;
    public const int RailAccess = 230;
    public const int HasScale = 359;

    // Target Pricing — Progress Rail.
    public const int TargetOffer = 336;
    public const int TargetUom = 337;
    public const int TrucksPerWeek = 339;
    public const int TargetFreightPerUom = 340;
    public const int TargetFreightCost = 341;
    public const int TargetBreakEven = 342;
    public const int PriceInNetTons = 345;
    public const int PriceInLbs = 347;
    public const int PriceInCwt = 348;
    public const int PriceInGrossTons = 349;
    public const int TargetMaterial = 352;
    public const int TargetPoNumber = 358;
    public const int PriceChangeFromPrior = 363;

    public const int MainEmail = 301;
    public const int PaymentTerms = 320;

    /// <summary>
    /// Dead Freight (checkbox): checked is Not Exempt, unchecked Exempt. The
    /// legacy integration calls it <c>DeadFreightExempt</c>, but checked does not mean exempt.
    /// </summary>
    public const int DeadFreight = 321;
    public const int TargetConsumerPrice = 346;
    public const int MainContactPhone = 355;

    /// <summary>The detail view's fields, in the order the portal's original query asked for them.</summary>
    public static readonly IReadOnlyList<int> DetailFields =
    [
        RecordId, Account, StreetAddress, City, State, Country, ZipCode, MainContactPhone, MainContactNames,
        PaymentTerms, MainEmail, LeadAssignedTo, RelevantConsumerDistances, InStockItemRecords, TotalActivities,
        TargetConsumerPrice, DeliveredLast90Days, DeliveredBefore90Days, DeadFreight,
    ];

    /// <summary>The call and prospect status view's fields.</summary>
    public static readonly IReadOnlyList<int> CallProspectStatusFields =
    [
        RecordId, ContactWithDecisionMakerMade, ProspectStatus, LastCallResult, SupplierObjections, CallBackDate,
        ObjectionExplained, CallNotes,
    ];

    /// <summary>The yard capabilities view's fields, in the order the portal's query asks for them.</summary>
    public static readonly IReadOnlyList<int> YardCapabilitiesFields =
    [
        RecordId, CrusherOnSite, LoggerOnSite, LoadFlatbeds, LoadDumps, MobileCrusher, CanExport, HasGaylordBoxes,
        BalerOnSite, HasScale, LoadVanTrailers, HasLoadWrap, UsesOwnTrucks, RailAccess,
    ];

    /// <summary>The Target Pricing — Progress Rail view's fields, in the order the portal's query asks for them.</summary>
    public static readonly IReadOnlyList<int> TargetPricingProgressRailFields =
    [
        RecordId, TargetMaterial, TargetBreakEven, TargetOffer, TargetUom, TrucksPerWeek, TargetFreightPerUom,
        TargetFreightCost, TargetConsumerPrice, PriceInNetTons, PriceInLbs, PriceInCwt, PriceInGrossTons,
        TargetPoNumber, PriceChangeFromPrior,
    ];

    /// <summary>The list view's fields.</summary>
    public static readonly IReadOnlyList<int> ListFields = [RecordId, Account];

    /// <summary>
    /// <c>{3.EX.'17511'}</c>: exactly this record.
    /// </summary>
    public static string ByRecordId(int recordId) => $"{{{RecordId}.EX.'{recordId}'}}";

    /// <summary>
    /// Suppliers with a name (<c>{8.XEX.''}</c>), optionally also containing
    /// <paramref name="search"/> (<c>{8.CT.'...'}</c>).
    /// </summary>
    public static string NamedLike(string? search) =>
        search is null
            ? $"{{{Account}.XEX.''}}"
            : $"{{{Account}.XEX.''}}AND{{{Account}.CT.'{EscapeValue(search)}'}}";

    /// <summary>Quickbase query values are single-quoted; a backslash escapes a quote or another backslash.</summary>
    public static string EscapeValue(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");
}
