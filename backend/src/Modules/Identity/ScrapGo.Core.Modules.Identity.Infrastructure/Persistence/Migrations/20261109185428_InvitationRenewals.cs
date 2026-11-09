using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvitationRenewals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_renewed_at",
                schema: "identity",
                table: "invitations",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "renew_count",
                schema: "identity",
                table: "invitations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitations_renew_count",
                schema: "identity",
                table: "invitations",
                sql: "renew_count >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_invitations_renew_count",
                schema: "identity",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "last_renewed_at",
                schema: "identity",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "renew_count",
                schema: "identity",
                table: "invitations");
        }
    }
}
