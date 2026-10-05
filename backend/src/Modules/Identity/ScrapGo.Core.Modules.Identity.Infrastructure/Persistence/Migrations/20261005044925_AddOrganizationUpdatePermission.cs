using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Appends <c>Organization.Update</c> to the permission catalog as id 18
    /// (ids are positional in <c>Permissions.All</c>; appended, never reordered)
    /// and grants it to the built-in <c>OrganizationAdministrator</c> (role id 1).
    /// <c>PlatformAdministrator</c> does not get it: renaming is an
    /// organization's own administration.
    /// </summary>
    public partial class AddOrganizationUpdatePermission : Migration
    {
        private const int OrganizationUpdatePermissionId = 18;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "identity",
                table: "permissions",
                columns: new[] { "id", "created_at", "name", "updated_at" },
                values: new object[] { 18, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Organization.Update", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.Sql($"""
                INSERT INTO identity.role_permissions (role_id, permission_id)
                VALUES (1, {OrganizationUpdatePermissionId})
                ON CONFLICT (role_id, permission_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Every grant of it, not just role 1's: custom roles may have been
            // composed with it since, and the catalog row can't go while any
            // role_permissions row still references it.
            migrationBuilder.Sql($"""
                DELETE FROM identity.role_permissions WHERE permission_id = {OrganizationUpdatePermissionId};
                """);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 18);
        }
    }
}
