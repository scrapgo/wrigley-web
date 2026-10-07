using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Retires the generic <c>Invoice.*</c> / <c>Report.*</c> permissions
    /// (catalog ids 10–16; ORG-APP-MODULE-MODEL.md, Decision 7). Every grant of
    /// them is removed from every role, built-in or custom. Each removal is
    /// written to the audit log as a system <c>role_permission_detached</c> with
    /// reason <c>permission_retired</c>, since migrations can't go through
    /// <c>IAuditLog</c>. The catalog rows stay: ids are positional and are
    /// never deleted or reused.
    /// </summary>
    public partial class RetireGenericPermissions : Migration
    {
        private const string RetiredIds = "10, 11, 12, 13, 14, 15, 16";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO audit.audit_logs (event_type, user_id, organization_id, actor_type, metadata, event_time, exported)
                SELECT 'role_permission_detached', NULL, r.organization_id, 'System',
                       jsonb_build_object('roleId', rp.role_id, 'permissionName', p.name, 'reason', 'permission_retired'),
                       now(), false
                FROM identity.role_permissions rp
                JOIN identity.roles r ON r.id = rp.role_id
                JOIN identity.permissions p ON p.id = rp.permission_id
                WHERE rp.permission_id IN ({RetiredIds});
                """);

            migrationBuilder.Sql($"""
                DELETE FROM identity.role_permissions WHERE permission_id IN ({RetiredIds});
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the built-in OrganizationAdministrator's grants only.
            // Grants removed from custom roles are recorded in the audit log
            // (reason permission_retired) but not restored.
            migrationBuilder.Sql($"""
                INSERT INTO identity.role_permissions (role_id, permission_id)
                SELECT 1, p.id FROM identity.permissions p WHERE p.id IN ({RetiredIds})
                ON CONFLICT (role_id, permission_id) DO NOTHING;
                """);
        }
    }
}
