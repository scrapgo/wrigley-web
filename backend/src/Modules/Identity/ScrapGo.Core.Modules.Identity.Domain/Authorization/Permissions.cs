namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// The permission catalog. It is a compile-time contract: seeded into
/// <c>identity.permissions</c> by migration (ids are 1-based positions in
/// <see cref="All"/>), and read-only over HTTP.
/// </summary>
/// <remarks>
/// Append new permissions to the end of <see cref="All"/>, never reorder:
/// seeded ids are positional, and a reorder would re-key existing grants.
/// </remarks>
public static class Permissions
{
    public const string UserRead = "User.Read";
    public const string UserCreate = "User.Create";
    public const string UserUpdate = "User.Update";
    public const string UserDelete = "User.Delete";

    public const string RoleRead = "Role.Read";
    public const string RoleCreate = "Role.Create";
    public const string RoleUpdate = "Role.Update";
    public const string RoleDelete = "Role.Delete";
    public const string RoleAssign = "Role.Assign";

    public const string InvoiceRead = "Invoice.Read";
    public const string InvoiceCreate = "Invoice.Create";
    public const string InvoiceUpdate = "Invoice.Update";
    public const string InvoiceDelete = "Invoice.Delete";
    public const string InvoiceApprove = "Invoice.Approve";

    public const string ReportRead = "Report.Read";
    public const string ReportExport = "Report.Export";

    public const string AdminAccess = "Admin.Access";

    /// <summary>Rename and otherwise edit an organization's own details. Permission id 18.</summary>
    public const string OrganizationUpdate = "Organization.Update";

    /// <summary>Create organizations (platform scope). Permission id 19.</summary>
    public const string OrganizationCreate = "Organization.Create";

    /// <summary>Deactivate and reactivate organizations (platform scope). Permission id 20.</summary>
    public const string OrganizationDeactivate = "Organization.Deactivate";


    public static readonly IReadOnlyList<string> All =
    [
        UserRead, UserCreate, UserUpdate, UserDelete,
        RoleRead, RoleCreate, RoleUpdate, RoleDelete, RoleAssign,
        InvoiceRead, InvoiceCreate, InvoiceUpdate, InvoiceDelete, InvoiceApprove,
        ReportRead, ReportExport,
        AdminAccess,
        OrganizationUpdate,
        OrganizationCreate, OrganizationDeactivate,
    ];

    /// <summary>
    /// Retired generic permissions (permission ids 10–16). Their rows stay,
    /// because ids are positional and must never be reused, but they can't be
    /// attached to a role, aren't listed, and never resolve.
    /// </summary>
    public static readonly string[] Retired =
    [
        InvoiceRead, InvoiceCreate, InvoiceUpdate, InvoiceDelete, InvoiceApprove,
        ReportRead, ReportExport,
    ];

    /// <summary>
    /// Permissions only meaningful at platform scope, held by
    /// <see cref="DefaultRoleNames.PlatformAdministrator"/> and never by an
    /// organization-level role.
    /// </summary>
    public static readonly string[] PlatformOnly =
        [AdminAccess, OrganizationCreate, OrganizationDeactivate];

    public static bool IsRetired(string permissionName) => Retired.Contains(permissionName, StringComparer.Ordinal);

    /// <summary>The live catalog: <see cref="All"/> without the <see cref="Retired"/> entries.</summary>
    public static IEnumerable<string> Active => All.Where(name => !IsRetired(name));
}
