namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// The roles, features, permissions and role-permission matrix seeded by
/// migration, exactly as in ACCESS-MODEL.md section 6. The configurations turn
/// it into <c>HasData</c>; ids are fixed so the seed is stable across migrations.
/// </summary>
internal static class AccessModelSeed
{
    public static readonly DateTimeOffset SeededAt = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    public sealed record RoleSeed(int Id, string Key, string Name, string Description, UserType UserType);

    public sealed record FeatureSeed(int Id, string Key, string Name, FeatureState State, bool IsToggleable);

    public sealed record PermissionSeed(int Id, string Key, string FeatureKey, PermissionAudience Audience, string Name);

    public static readonly IReadOnlyList<RoleSeed> Roles =
    [
        new(1, DefaultRoleNames.SiteAdmin, "Site Admin", "A few ScrapGo staff with full administration.", UserType.Internal),
        new(2, DefaultRoleNames.ScrapGoUser, "ScrapGo User", "Every internal user, assigned on first sign-in.", UserType.Internal),
        new(3, DefaultRoleNames.User, "User", "Buyers, sellers and transporters. Joins by invitation only.", UserType.External),
    ];

    public static readonly IReadOnlyList<FeatureSeed> Features =
    [
        new(1, FeatureKeys.Dashboard, "Dashboard", FeatureState.On, IsToggleable: true),
        new(2, FeatureKeys.Suppliers, "Suppliers", FeatureState.On, IsToggleable: true),
        new(3, FeatureKeys.SupplierEdit, "Supplier editing", FeatureState.Targeted, IsToggleable: true),
        new(4, FeatureKeys.Users, "Users", FeatureState.On, IsToggleable: false),
        new(5, FeatureKeys.Roles, "Roles", FeatureState.On, IsToggleable: false),
        new(6, FeatureKeys.Features, "Features", FeatureState.On, IsToggleable: false),
        new(7, FeatureKeys.Audit, "Audit", FeatureState.On, IsToggleable: false),
    ];

    public static readonly IReadOnlyList<PermissionSeed> PermissionSeeds =
    [
        new(1, Permissions.DashboardView, FeatureKeys.Dashboard, PermissionAudience.Any, "View the dashboard"),
        new(2, Permissions.SuppliersViewAll, FeatureKeys.Suppliers, PermissionAudience.Internal, "View all suppliers"),
        new(3, Permissions.SuppliersViewOwn, FeatureKeys.Suppliers, PermissionAudience.External, "View linked suppliers"),
        new(4, Permissions.SuppliersEditAll, FeatureKeys.SupplierEdit, PermissionAudience.Internal, "Edit all suppliers"),
        new(5, Permissions.SuppliersEditOwn, FeatureKeys.SupplierEdit, PermissionAudience.External, "Edit linked suppliers"),
        new(6, Permissions.UsersView, FeatureKeys.Users, PermissionAudience.Internal, "View users"),
        new(7, Permissions.UsersInvite, FeatureKeys.Users, PermissionAudience.Internal, "Invite users"),
        new(8, Permissions.UsersManage, FeatureKeys.Users, PermissionAudience.Internal, "Manage users"),
        new(9, Permissions.RolesView, FeatureKeys.Roles, PermissionAudience.Internal, "View roles"),
        new(10, Permissions.RolesManage, FeatureKeys.Roles, PermissionAudience.Internal, "Manage roles"),
        new(11, Permissions.FeaturesView, FeatureKeys.Features, PermissionAudience.Internal, "View features"),
        new(12, Permissions.FeaturesManage, FeatureKeys.Features, PermissionAudience.Internal, "Manage features"),
        new(13, Permissions.AuditView, FeatureKeys.Audit, PermissionAudience.Internal, "View the audit log"),
    ];

    /// <summary>Role key → the permission keys it holds.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Matrix = new Dictionary<string, string[]>
    {
        [DefaultRoleNames.SiteAdmin] =
        [
            Permissions.DashboardView, Permissions.SuppliersViewAll, Permissions.SuppliersEditAll,
            Permissions.UsersView, Permissions.UsersInvite, Permissions.UsersManage,
            Permissions.RolesView, Permissions.RolesManage,
            Permissions.FeaturesView, Permissions.FeaturesManage,
            Permissions.AuditView,
        ],
        [DefaultRoleNames.ScrapGoUser] =
        [
            Permissions.DashboardView, Permissions.SuppliersViewAll, Permissions.SuppliersEditAll, Permissions.UsersInvite,
        ],
        [DefaultRoleNames.User] = [Permissions.DashboardView, Permissions.SuppliersViewOwn],
    };

    public static int RoleId(string key) => Roles.Single(r => r.Key == key).Id;

    public static int FeatureId(string key) => Features.Single(f => f.Key == key).Id;

    public static int PermissionId(string key) => PermissionSeeds.Single(p => p.Key == key).Id;
}
