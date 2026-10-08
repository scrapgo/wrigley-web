namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// The catalog permissions supplier endpoints check. They're defined in the
/// Identity module's catalog (Downstream application, Suppliers module) and
/// named here by string, because modules never reference each other; a spec
/// pins them to the catalog.
/// </summary>
public static class SupplierPermissions
{
    public const string Read = "Downstream.Suppliers.Read";

    /// <summary>
    /// Platform-wide supplier access (no organization or application in the
    /// route) is for platform administrators: <c>Admin.Access</c> at platform
    /// scope, which also requires a Google Workspace sign-in.
    /// </summary>
    public const string PlatformRead = "Admin.Access";
}
