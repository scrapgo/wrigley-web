namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// The permission supplier endpoints check. It's defined in the Identity
/// module's catalog and named here by string, because modules never reference
/// each other.
/// </summary>
public static class SupplierPermissions
{
    /// <summary>
    /// Reading every supplier: internal roles hold it. External users' scoped
    /// <c>suppliers.view_own</c> arrives with supplier scoping (B13).
    /// </summary>
    public const string ViewAll = "suppliers.view_all";
}
