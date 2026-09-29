using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ScrapGo.Core.Shared.Infrastructure.Audit.Migrations
{
    /// <inheritdoc />
    public partial class InitialAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    actor_type = table.Column<string>(type: "text", nullable: false, defaultValue: "Human"),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    service_principal_id = table.Column<int>(type: "integer", nullable: true),
                    organization_id = table.Column<int>(type: "integer", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    event_time = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    exported = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.CheckConstraint("ck_audit_logs_actor_type", "actor_type IN ('Human', 'Service', 'System')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_event_time",
                schema: "audit",
                table: "audit_logs",
                column: "event_time");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_event_type",
                schema: "audit",
                table: "audit_logs",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_organization_id",
                schema: "audit",
                table: "audit_logs",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_service_principal_id",
                schema: "audit",
                table: "audit_logs",
                column: "service_principal_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_id",
                schema: "audit",
                table: "audit_logs",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "audit");
        }
    }
}
