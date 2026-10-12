using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>An in-stock item's material (Quickbase In-Stock Items field 6, dropdown), sent and accepted as its exact text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MaterialType>))]
public enum MaterialType
{
    [JsonStringEnumMemberName("Aluminum Radiators")]
    AluminumRadiators,

    [JsonStringEnumMemberName("Auto Batteries")]
    AutoBatteries,

    [JsonStringEnumMemberName("Harness wire")]
    HarnessWire,

    [JsonStringEnumMemberName("Alternators")]
    Alternators,

    [JsonStringEnumMemberName("Starters")]
    Starters,

    [JsonStringEnumMemberName("Crushed Cars with Motors")]
    CrushedCarsWithMotors,

    [JsonStringEnumMemberName("Crushed Cars without Motors")]
    CrushedCarsWithoutMotors,

    [JsonStringEnumMemberName("All Al Motors")]
    AllAlMotors,

    [JsonStringEnumMemberName("Cast Iron Motors")]
    CastIronMotors,

    [JsonStringEnumMemberName("All Al Engine Combos")]
    AllAlEngineCombos,

    [JsonStringEnumMemberName("Cast Iron Combos")]
    CastIronCombos,

    [JsonStringEnumMemberName("Aluminum Wheels")]
    AluminumWheels,

    [JsonStringEnumMemberName("Sheet Metal")]
    SheetMetal,

    [JsonStringEnumMemberName("Prepared Steel")]
    PreparedSteel,
}

/// <summary>The unit an in-stock item's weight is in (Quickbase In-Stock Items field 8, dropdown), sent and accepted as its exact text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UnitOfMeasure>))]
public enum UnitOfMeasure
{
    [JsonStringEnumMemberName("NT")]
    Nt,

    [JsonStringEnumMemberName("Pieces")]
    Pieces,

    [JsonStringEnumMemberName("LBS")]
    Lbs,

    [JsonStringEnumMemberName("GT")]
    Gt,

    [JsonStringEnumMemberName("MT")]
    Mt,
}

/// <summary>How an in-stock item is prepared (Quickbase In-Stock Items field 10, dropdown), sent and accepted as its exact text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Preparation>))]
public enum Preparation
{
    [JsonStringEnumMemberName("Bales")]
    Bales,

    [JsonStringEnumMemberName("Boxed")]
    Boxed,

    [JsonStringEnumMemberName("Crushed/Flattened")]
    CrushedFlattened,

    [JsonStringEnumMemberName("Logged")]
    Logged,

    [JsonStringEnumMemberName("Loose")]
    Loose,

    [JsonStringEnumMemberName("Loose or Bailed")]
    LooseOrBailed,

    [JsonStringEnumMemberName("Loose or Logged")]
    LooseOrLogged,

    [JsonStringEnumMemberName("Loose or Palletized")]
    LooseOrPalletized,

    [JsonStringEnumMemberName("Unprepared")]
    Unprepared,

    [JsonStringEnumMemberName("Wrapped/Palletized")]
    WrappedPalletized,
}

/// <summary>Whether an in-stock item is available continuously or once. "Continous" is Quickbase's spelling (Quickbase In-Stock Items field 13, dropdown), sent and accepted as its exact text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Availability>))]
public enum Availability
{
    [JsonStringEnumMemberName("Continous")]
    Continuous,

    [JsonStringEnumMemberName("One Time")]
    OneTime,
}

/// <summary>The truck an in-stock item needs (Quickbase In-Stock Items field 18, dropdown), sent and accepted as its exact text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TruckType>))]
public enum TruckType
{
    [JsonStringEnumMemberName("Dump Trailer")]
    DumpTrailer,

    [JsonStringEnumMemberName("Van Trailer")]
    VanTrailer,

    [JsonStringEnumMemberName("Flatbed Trailer")]
    FlatbedTrailer,

    [JsonStringEnumMemberName("Stepdeck Trailer")]
    StepdeckTrailer,

    [JsonStringEnumMemberName("Other Specialized Trailer")]
    OtherSpecializedTrailer,

    [JsonStringEnumMemberName("Flatbread Trailer")]
    FlatbreadTrailer,
}

/// <summary>Whether an in-stock item is dirty or clean (Quickbase In-Stock Items field 23, dropdown), sent and accepted as its exact text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DirtyOrClean>))]
public enum DirtyOrClean
{
    [JsonStringEnumMemberName("Dirty")]
    Dirty,

    [JsonStringEnumMemberName("Clean")]
    Clean,
}

/// <summary>The in-stock item dropdowns. Labels come from each member's <see cref="JsonStringEnumMemberNameAttribute"/>.</summary>
public static class InStockItemDropdowns
{
    public static DropdownCatalog<MaterialType> MaterialTypes { get; } = DropdownCatalog<MaterialType>.FromJsonNames();

    public static DropdownCatalog<UnitOfMeasure> UnitsOfMeasure { get; } = DropdownCatalog<UnitOfMeasure>.FromJsonNames();

    public static DropdownCatalog<Preparation> Preparations { get; } = DropdownCatalog<Preparation>.FromJsonNames();

    public static DropdownCatalog<Availability> Availabilities { get; } = DropdownCatalog<Availability>.FromJsonNames();

    public static DropdownCatalog<TruckType> TruckTypes { get; } = DropdownCatalog<TruckType>.FromJsonNames();

    public static DropdownCatalog<DirtyOrClean> DirtyOrClean { get; } = DropdownCatalog<DirtyOrClean>.FromJsonNames();
}
