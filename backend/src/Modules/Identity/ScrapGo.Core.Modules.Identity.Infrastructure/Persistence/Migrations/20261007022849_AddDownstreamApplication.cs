using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the first catalog application, Downstream (id 1), with modules
    /// Pricing, Opportunities, Loads &amp; Freight and Suppliers (101–104), their
    /// permissions (catalog ids 25–32), and the role templates "Downstream
    /// Administrator" and "Downstream Viewer" (DownstreamApplication). Nothing is
    /// assigned to any organization or granted to anyone.
    /// </summary>
    public partial class AddDownstreamApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "identity",
                table: "applications",
                columns: new[] { "id", "created_at", "key", "name", "updated_at" },
                values: new object[] { 1, new DateTimeOffset(new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "downstream", "Downstream", new DateTimeOffset(new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "modules",
                columns: new[] { "id", "application_id", "created_at", "key", "name", "updated_at" },
                values: new object[,]
                {
                    { 101, 1, new DateTimeOffset(new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "pricing", "Pricing", new DateTimeOffset(new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 102, 1, new DateTimeOffset(new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "opportunities", "Opportunities", new DateTimeOffset(new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 103, 1, new DateTimeOffset(new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "loads", "Loads & Freight", new DateTimeOffset(new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 104, 1, new DateTimeOffset(new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "suppliers", "Suppliers", new DateTimeOffset(new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "permissions",
                columns: new[] { "id", "created_at", "module_id", "name", "updated_at" },
                values: new object[,]
                {
                    { 25, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 101, "Downstream.Pricing.Read", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 26, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 101, "Downstream.Pricing.Write", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 27, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 102, "Downstream.Opportunities.Read", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 28, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 102, "Downstream.Opportunities.Write", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 29, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 103, "Downstream.Loads.Read", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 30, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 103, "Downstream.Loads.Write", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 31, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 104, "Downstream.Suppliers.Read", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 32, new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 104, "Downstream.Suppliers.Write", new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            // Role templates (organization_id null, application_id 1). Role ids
            // are database-generated, so they're seeded by name.
            migrationBuilder.Sql("""
                INSERT INTO identity.roles (name, description, organization_id, application_id, status, created_at, updated_at)
                SELECT t.name, t.description, NULL, 1, 'Active', TIMESTAMPTZ '2026-10-05 00:00:00+00', TIMESTAMPTZ '2026-10-05 00:00:00+00'
                FROM (VALUES
                    ('Downstream Administrator', 'Grants and revokes Downstream access in an organization, with every Downstream permission.'),
                    ('Downstream Viewer', 'Read-only access to every Downstream module.')
                ) AS t(name, description)
                WHERE NOT EXISTS (SELECT 1 FROM identity.roles r WHERE r.name = t.name AND r.organization_id IS NULL);

                INSERT INTO identity.role_permissions (role_id, permission_id)
                SELECT r.id, p.id
                FROM identity.roles r
                JOIN identity.permissions p ON p.name IN (
                    'Application.ManageAccess',
                    'Downstream.Pricing.Read', 'Downstream.Pricing.Write',
                    'Downstream.Opportunities.Read', 'Downstream.Opportunities.Write',
                    'Downstream.Loads.Read', 'Downstream.Loads.Write',
                    'Downstream.Suppliers.Read', 'Downstream.Suppliers.Write')
                WHERE r.name = 'Downstream Administrator' AND r.organization_id IS NULL AND r.application_id = 1
                ON CONFLICT (role_id, permission_id) DO NOTHING;

                INSERT INTO identity.role_permissions (role_id, permission_id)
                SELECT r.id, p.id
                FROM identity.roles r
                JOIN identity.permissions p ON p.name IN (
                    'Downstream.Pricing.Read', 'Downstream.Opportunities.Read',
                    'Downstream.Loads.Read', 'Downstream.Suppliers.Read')
                WHERE r.name = 'Downstream Viewer' AND r.organization_id IS NULL AND r.application_id = 1
                ON CONFLICT (role_id, permission_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only possible before anything references Downstream (grants,
            // entitlements, custom roles): those foreign keys are Restrict.
            migrationBuilder.Sql("""
                DELETE FROM identity.role_permissions
                WHERE role_id IN (SELECT id FROM identity.roles WHERE application_id = 1 AND organization_id IS NULL);
                DELETE FROM identity.roles WHERE application_id = 1 AND organization_id IS NULL;
                """);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "modules",
                keyColumn: "id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "modules",
                keyColumn: "id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "modules",
                keyColumn: "id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "modules",
                keyColumn: "id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "applications",
                keyColumn: "id",
                keyValue: 1);
        }
    }
}
