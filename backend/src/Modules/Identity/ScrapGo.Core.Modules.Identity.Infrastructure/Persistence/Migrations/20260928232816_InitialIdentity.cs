using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    identity_platform_uid = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    classification = table.Column<string>(type: "text", nullable: false, defaultValue: "External"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_classification", "classification IN ('External', 'Internal')");
                    table.CheckConstraint("ck_users_status", "status IN ('Active', 'Disabled')");
                });

            migrationBuilder.CreateTable(
                name: "linked_credentials",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    identity_provider_id = table.Column<int>(type: "integer", nullable: true),
                    provider_name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_linked_credentials", x => x.id);
                    table.CheckConstraint("ck_linked_credentials_status", "status IN ('Active', 'Orphaned')");
                    table.ForeignKey(
                        name: "fk_linked_credentials_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_linked_credentials_identity_provider_id",
                schema: "identity",
                table: "linked_credentials",
                column: "identity_provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_linked_credentials_status",
                schema: "identity",
                table: "linked_credentials",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_linked_credentials_user_id_provider_name",
                schema: "identity",
                table: "linked_credentials",
                columns: new[] { "user_id", "provider_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_classification",
                schema: "identity",
                table: "users",
                column: "classification");

            migrationBuilder.CreateIndex(
                name: "ix_users_status",
                schema: "identity",
                table: "users",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_users_identity_platform_uid",
                schema: "identity",
                table: "users",
                column: "identity_platform_uid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "linked_credentials",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");
        }
    }
}
