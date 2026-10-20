namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// Every permission key (ACCESS-MODEL.md section 6). A compile-time contract,
/// seeded by migration: admins map keys to roles, they never invent keys. Code
/// checks these keys, never role names.
/// </summary>
public static class Permissions
{
    public const string DashboardView = "dashboard.view";

    public const string SuppliersViewAll = "suppliers.view_all";

    public const string SuppliersViewOwn = "suppliers.view_own";

    public const string SuppliersEditAll = "suppliers.edit_all";

    /// <summary>Defined but not granted in V1: external users are view-only.</summary>
    public const string SuppliersEditOwn = "suppliers.edit_own";

    public const string UsersView = "users.view";

    public const string UsersInvite = "users.invite";

    /// <summary>Enable/disable users, change their roles and supplier links.</summary>
    public const string UsersManage = "users.manage";

    public const string RolesView = "roles.view";

    public const string RolesManage = "roles.manage";

    public const string FeaturesView = "features.view";

    public const string FeaturesManage = "features.manage";

    public const string AuditView = "audit.view";

    public static readonly IReadOnlyList<string> All =
    [
        DashboardView,
        SuppliersViewAll, SuppliersViewOwn, SuppliersEditAll, SuppliersEditOwn,
        UsersView, UsersInvite, UsersManage,
        RolesView, RolesManage,
        FeaturesView, FeaturesManage,
        AuditView,
    ];
}
