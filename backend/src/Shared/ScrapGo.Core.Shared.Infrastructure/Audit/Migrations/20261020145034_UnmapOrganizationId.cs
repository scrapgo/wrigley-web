using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrapGo.Core.Shared.Infrastructure.Audit.Migrations
{
    /// <summary>
    /// Organizations are gone, so the model no longer maps <c>organization_id</c>.
    /// The column is deliberately kept (nullable, unmapped) so past audit rows
    /// keep their organization; only its index is dropped.
    /// </summary>
    public partial class UnmapOrganizationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_logs_organization_id",
                schema: "audit",
                table: "audit_logs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_organization_id",
                schema: "audit",
                table: "audit_logs",
                column: "organization_id");
        }
    }
}
