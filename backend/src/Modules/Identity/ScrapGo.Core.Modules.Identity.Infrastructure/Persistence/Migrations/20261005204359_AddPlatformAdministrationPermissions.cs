using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Appends the platform administration permissions (catalog ids 19–23) and
    /// <c>Application.ManageAccess</c> (24) for the organization → application →
    /// module model (ORG-APP-MODULE-MODEL.md), and grants 19–23 to
    /// PlatformAdministrator.
    /// </summary>
    public partial class AddPlatformAdministrationPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "identity",
                table: "permissions",
                columns: new[] { "id", "created_at", "name", "updated_at" },
                values: new object[,]
                {
                    { 19, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Organization.Create", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 20, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Organization.Deactivate", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 21, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Application.Assign", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 22, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Module.Manage", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 23, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Catalog.Manage", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 24, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Application.ManageAccess", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            // Platform administration permissions go to PlatformAdministrator
            // only. Application.ManageAccess (24) goes to no built-in role: it is
            // held through each application's administrator template.
            migrationBuilder.Sql("""
                INSERT INTO identity.role_permissions (role_id, permission_id)
                SELECT r.id, p.id
                FROM identity.roles r
                JOIN identity.permissions p ON p.id IN (19, 20, 21, 22, 23)
                WHERE r.name = 'PlatformAdministrator' AND r.organization_id IS NULL
                ON CONFLICT (role_id, permission_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM identity.role_permissions WHERE permission_id IN (19, 20, 21, 22, 23, 24);
                """);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 24);
        }
    }
}
