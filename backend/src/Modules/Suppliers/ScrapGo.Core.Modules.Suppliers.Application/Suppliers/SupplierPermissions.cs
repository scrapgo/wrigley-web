namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// The permissions supplier endpoints check. They're defined in the Identity
/// module's catalog and named here by string, because modules never reference
/// each other; a spec pins them to the catalog.
/// </summary>
public static class SupplierPermissions
{
    /// <summary>Every supplier (internal roles).</summary>
    public const string ViewAll = "suppliers.view_all";

    /// <summary>Only the caller's linked suppliers (the external <c>user</c> role).</summary>
    public const string ViewOwn = "suppliers.view_own";
}
