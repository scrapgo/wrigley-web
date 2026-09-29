using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialQuickbaseEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "quickbase");

            migrationBuilder.CreateTable(
                name: "query_caches",
                schema: "quickbase",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    query_hash = table.Column<string>(type: "text", nullable: false),
                    table_id = table.Column<string>(type: "text", nullable: false),
                    request_json = table.Column<string>(type: "jsonb", nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_query_caches", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_query_caches_fetched_at",
                schema: "quickbase",
                table: "query_caches",
                column: "fetched_at");

            migrationBuilder.CreateIndex(
                name: "ix_query_caches_table_id",
                schema: "quickbase",
                table: "query_caches",
                column: "table_id");

            migrationBuilder.CreateIndex(
                name: "ux_query_caches_query_hash",
                schema: "quickbase",
                table: "query_caches",
                column: "query_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "query_caches",
                schema: "quickbase");
        }
    }
}
