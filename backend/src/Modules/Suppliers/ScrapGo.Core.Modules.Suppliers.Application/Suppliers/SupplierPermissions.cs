namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// The permission supplier endpoints check. It's defined in the Identity
/// module's catalog and named here by string, because modules never reference
/// each other.
/// </summary>
public static class SupplierPermissions
{
    /// <summary>
    /// Supplier access is for platform administrators for now: <c>Admin.Access</c>
    /// at platform scope, which also requires a Google Workspace sign-in.
    /// Supplier scoping (B13) replaces this.
    /// </summary>
    public const string PlatformRead = "Admin.Access";
}
