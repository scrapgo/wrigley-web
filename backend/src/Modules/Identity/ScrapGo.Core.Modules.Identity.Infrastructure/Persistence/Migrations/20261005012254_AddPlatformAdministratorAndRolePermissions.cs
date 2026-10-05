using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Fixes "Admin.Access is never granted" (ADMIN-API-STATUS.md, Task 1).
    /// Adds the built-in PlatformAdministrator role and seeds the built-in
    /// roles' permissions by catalog id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model is unchanged; this migration is data only. It is hand-written
    /// SQL rather than <c>HasData</c> because the shared database may already
    /// hold custom roles, so PlatformAdministrator can't be given a fixed id.
    /// Every statement is idempotent.
    /// </para>
    /// <para>
    /// Catalog ids are positional (<c>Permissions.All</c>, seeded in
    /// AddOrganizationsAndRbac):
    /// 1 User.Read, 2 User.Create, 3 User.Update, 4 User.Delete,
    /// 5 Role.Read, 6 Role.Create, 7 Role.Update, 8 Role.Delete, 9 Role.Assign,
    /// 10 Invoice.Read, 11 Invoice.Create, 12 Invoice.Update, 13 Invoice.Delete,
    /// 14 Invoice.Approve, 15 Report.Read, 16 Report.Export, 17 Admin.Access.
    /// </para>
    /// </remarks>
    public partial class AddPlatformAdministratorAndRolePermissions : Migration
    {
        // PlatformAdministrator: User.*, Role.* and Admin.Access.
        private const string PlatformAdministratorPermissionIds = "1, 2, 3, 4, 5, 6, 7, 8, 9, 17";

        // OrganizationAdministrator (role id 1): every catalog permission except
        // Admin.Access. Every assignment of it is organization-scoped, so none
        // of this reaches platform scope.
        private const string OrganizationAdministratorPermissionIds = "1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16";

        private const string PlatformAdministratorRole =
            "r.name = 'PlatformAdministrator' AND r.organization_id IS NULL";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Status takes its column default, 'Active'. ux_roles_name_platform_scope
            // backs up the NOT EXISTS.
            migrationBuilder.Sql($"""
                INSERT INTO identity.roles (organization_id, name, description, created_at, updated_at)
                SELECT NULL, 'PlatformAdministrator',
                       'ScrapGo staff with platform-wide administration. Assigned only at platform scope.',
                       now(), now()
                WHERE NOT EXISTS (SELECT 1 FROM identity.roles r WHERE {PlatformAdministratorRole});
                """);

            migrationBuilder.Sql($"""
                INSERT INTO identity.role_permissions (role_id, permission_id)
                SELECT r.id, p.id
                FROM identity.roles r
                JOIN identity.permissions p ON p.id IN ({PlatformAdministratorPermissionIds})
                WHERE {PlatformAdministratorRole}
                ON CONFLICT (role_id, permission_id) DO NOTHING;
                """);

            migrationBuilder.Sql($"""
                INSERT INTO identity.role_permissions (role_id, permission_id)
                SELECT 1, p.id
                FROM identity.permissions p
                WHERE p.id IN ({OrganizationAdministratorPermissionIds})
                ON CONFLICT (role_id, permission_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE FROM identity.role_permissions
                WHERE role_id = 1 AND permission_id IN ({OrganizationAdministratorPermissionIds});
                """);

            migrationBuilder.Sql($"""
                DELETE FROM identity.role_permissions rp
                USING identity.roles r
                WHERE rp.role_id = r.id
                  AND {PlatformAdministratorRole}
                  AND rp.permission_id IN ({PlatformAdministratorPermissionIds});
                """);

            // Never silently destroy a bootstrapped grant: the role row stays
            // while any user role references it (the FK would refuse anyway).
            migrationBuilder.Sql($"""
                DELETE FROM identity.roles r
                WHERE {PlatformAdministratorRole}
                  AND NOT EXISTS (SELECT 1 FROM identity.user_roles ur WHERE ur.role_id = r.id);
                """);
        }
    }
}
