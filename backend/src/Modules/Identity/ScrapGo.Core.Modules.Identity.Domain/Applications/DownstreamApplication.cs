namespace ScrapGo.Core.Modules.Identity.Domain.Applications;

/// <summary>
/// The Downstream portal as a catalog application: one module per portal
/// section. Permission names follow <c>{App}.{Module}.{Action}</c> (Decision 1)
/// and are appended to <see cref="Authorization.Permissions.All"/> (catalog ids 25–32).
/// </summary>
public static class DownstreamApplication
{
    public const int Id = 1;

    public const int PricingModuleId = 101;
    public const int OpportunitiesModuleId = 102;
    public const int LoadsModuleId = 103;
    public const int SuppliersModuleId = 104;

    public const string PricingRead = "Downstream.Pricing.Read";
    public const string PricingWrite = "Downstream.Pricing.Write";
    public const string OpportunitiesRead = "Downstream.Opportunities.Read";
    public const string OpportunitiesWrite = "Downstream.Opportunities.Write";
    public const string LoadsRead = "Downstream.Loads.Read";
    public const string LoadsWrite = "Downstream.Loads.Write";
    public const string SuppliersRead = "Downstream.Suppliers.Read";
    public const string SuppliersWrite = "Downstream.Suppliers.Write";

    public const string AdministratorRole = "Downstream Administrator";
    public const string ViewerRole = "Downstream Viewer";

    /// <summary>Every module permission, in catalog order.</summary>
    public static readonly IReadOnlyList<string> Permissions =
    [
        PricingRead, PricingWrite,
        OpportunitiesRead, OpportunitiesWrite,
        LoadsRead, LoadsWrite,
        SuppliersRead, SuppliersWrite,
    ];

    public static readonly ApplicationDefinition Definition = new(
        Id,
        "downstream",
        "Downstream",
        [
            new ModuleDefinition(PricingModuleId, "pricing", "Pricing", [PricingRead, PricingWrite]),
            new ModuleDefinition(OpportunitiesModuleId, "opportunities", "Opportunities", [OpportunitiesRead, OpportunitiesWrite]),
            new ModuleDefinition(LoadsModuleId, "loads", "Loads & Freight", [LoadsRead, LoadsWrite]),
            new ModuleDefinition(SuppliersModuleId, "suppliers", "Suppliers", [SuppliersRead, SuppliersWrite]),
        ],
        [
            new RoleTemplateDefinition(
                AdministratorRole,
                "Grants and revokes Downstream access in an organization, with every Downstream permission.",
                [Authorization.Permissions.ApplicationManageAccess, .. Permissions]),
            new RoleTemplateDefinition(
                ViewerRole,
                "Read-only access to every Downstream module.",
                [PricingRead, OpportunitiesRead, LoadsRead, SuppliersRead]),
        ]);
}
