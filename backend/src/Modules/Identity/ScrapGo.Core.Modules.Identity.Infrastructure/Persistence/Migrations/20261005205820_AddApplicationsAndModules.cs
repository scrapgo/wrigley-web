using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationsAndModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "application_id",
                schema: "identity",
                table: "user_roles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at",
                schema: "identity",
                table: "user_roles",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "application_id",
                schema: "identity",
                table: "roles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "module_id",
                schema: "identity",
                table: "permissions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "applications",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                    table.CheckConstraint("ck_applications_status", "status IN ('Active', 'Retired')");
                });

            migrationBuilder.CreateTable(
                name: "modules",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    application_id = table.Column<int>(type: "integer", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modules", x => x.id);
                    table.UniqueConstraint("ak_modules_application_id_id", x => new { x.application_id, x.id });
                    table.CheckConstraint("ck_modules_status", "status IN ('Active', 'Retired')");
                    table.ForeignKey(
                        name: "fk_modules_application_id",
                        column: x => x.application_id,
                        principalSchema: "identity",
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_applications",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    organization_id = table.Column<int>(type: "integer", nullable: false),
                    application_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    enabled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organization_applications", x => x.id);
                    table.UniqueConstraint("ak_organization_applications_id_application_id", x => new { x.id, x.application_id });
                    table.CheckConstraint("ck_organization_applications_status", "status IN ('Active', 'Removed')");
                    table.ForeignKey(
                        name: "fk_organization_applications_application_id",
                        column: x => x.application_id,
                        principalSchema: "identity",
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_organization_applications_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_application_modules",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    organization_application_id = table.Column<int>(type: "integer", nullable: false),
                    application_id = table.Column<int>(type: "integer", nullable: false),
                    module_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Enabled"),
                    enabled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    disabled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organization_application_modules", x => x.id);
                    table.CheckConstraint("ck_organization_application_modules_status", "status IN ('Enabled', 'Disabled')");
                    table.ForeignKey(
                        name: "fk_organization_application_modules_module",
                        columns: x => new { x.application_id, x.module_id },
                        principalSchema: "identity",
                        principalTable: "modules",
                        principalColumns: new[] { "application_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_organization_application_modules_organization_application",
                        columns: x => new { x.organization_application_id, x.application_id },
                        principalSchema: "identity",
                        principalTable: "organization_applications",
                        principalColumns: new[] { "id", "application_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 1,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 2,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 3,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 4,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 5,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 6,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 7,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 8,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 9,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 10,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 11,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 12,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 13,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 14,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 15,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 16,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 17,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 18,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 19,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 20,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 21,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 22,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 23,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "permissions",
                keyColumn: "id",
                keyValue: 24,
                column: "module_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "roles",
                keyColumn: "id",
                keyValue: 1,
                column: "application_id",
                value: null);

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_application_id",
                schema: "identity",
                table: "user_roles",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_organization_id_application_id",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "organization_id", "application_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_roles_application_scope",
                schema: "identity",
                table: "user_roles",
                sql: "application_id IS NULL OR organization_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_roles_application_id",
                schema: "identity",
                table: "roles",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_permissions_module_id",
                schema: "identity",
                table: "permissions",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "ux_applications_key",
                schema: "identity",
                table: "applications",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_modules_application_id_key",
                schema: "identity",
                table: "modules",
                columns: new[] { "application_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organization_application_modules_application_id_module_id",
                schema: "identity",
                table: "organization_application_modules",
                columns: new[] { "application_id", "module_id" });

            migrationBuilder.CreateIndex(
                name: "ix_organization_application_modules_organization_application_i",
                schema: "identity",
                table: "organization_application_modules",
                columns: new[] { "organization_application_id", "application_id" });

            migrationBuilder.CreateIndex(
                name: "ux_org_application_modules_org_application_id_module_id",
                schema: "identity",
                table: "organization_application_modules",
                columns: new[] { "organization_application_id", "module_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organization_applications_application_id",
                schema: "identity",
                table: "organization_applications",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ux_organization_applications_organization_id_application_id",
                schema: "identity",
                table: "organization_applications",
                columns: new[] { "organization_id", "application_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_permissions_module_id",
                schema: "identity",
                table: "permissions",
                column: "module_id",
                principalSchema: "identity",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_roles_application_id",
                schema: "identity",
                table: "roles",
                column: "application_id",
                principalSchema: "identity",
                principalTable: "applications",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_roles_application_id",
                schema: "identity",
                table: "user_roles",
                column: "application_id",
                principalSchema: "identity",
                principalTable: "applications",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_permissions_module_id",
                schema: "identity",
                table: "permissions");

            migrationBuilder.DropForeignKey(
                name: "fk_roles_application_id",
                schema: "identity",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "fk_user_roles_application_id",
                schema: "identity",
                table: "user_roles");

            migrationBuilder.DropTable(
                name: "organization_application_modules",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "modules",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "organization_applications",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "applications",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "ix_user_roles_application_id",
                schema: "identity",
                table: "user_roles");

            migrationBuilder.DropIndex(
                name: "ix_user_roles_organization_id_application_id",
                schema: "identity",
                table: "user_roles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_roles_application_scope",
                schema: "identity",
                table: "user_roles");

            migrationBuilder.DropIndex(
                name: "ix_roles_application_id",
                schema: "identity",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_permissions_module_id",
                schema: "identity",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "application_id",
                schema: "identity",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "expires_at",
                schema: "identity",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "application_id",
                schema: "identity",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "module_id",
                schema: "identity",
                table: "permissions");
        }
    }
}
