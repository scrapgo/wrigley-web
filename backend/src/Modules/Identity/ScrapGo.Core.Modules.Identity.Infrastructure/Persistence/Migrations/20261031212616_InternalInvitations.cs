using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InternalInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Invitations gain a user type and several roles. Existing rows are
            // External with their one role, copied over before role_id goes.
            migrationBuilder.AddColumn<string>(
                name: "user_type",
                schema: "identity",
                table: "invitations",
                type: "text",
                nullable: false,
                defaultValue: "External");

            migrationBuilder.AddColumn<List<int>>(
                name: "role_ids",
                schema: "identity",
                table: "invitations",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.Sql("UPDATE identity.invitations SET role_ids = ARRAY[role_id];");

            migrationBuilder.Sql("ALTER TABLE identity.invitations ALTER COLUMN role_ids DROP DEFAULT;");

            migrationBuilder.Sql("ALTER TABLE identity.invitations ALTER COLUMN user_type DROP DEFAULT;");

            migrationBuilder.DropForeignKey(
                name: "fk_invitations_role_id",
                schema: "identity",
                table: "invitations");

            migrationBuilder.DropIndex(
                name: "ix_invitations_role_id",
                schema: "identity",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "role_id",
                schema: "identity",
                table: "invitations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitations_role_ids",
                schema: "identity",
                table: "invitations",
                sql: "cardinality(role_ids) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitations_user_type",
                schema: "identity",
                table: "invitations",
                sql: "user_type IN ('Internal', 'External')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to one role per invitation: internal invitations can't be
            // represented, so they are removed; the rest keep their first role.
            migrationBuilder.Sql("DELETE FROM identity.invitations WHERE user_type = 'Internal';");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invitations_role_ids",
                schema: "identity",
                table: "invitations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invitations_user_type",
                schema: "identity",
                table: "invitations");

            migrationBuilder.AddColumn<int>(
                name: "role_id",
                schema: "identity",
                table: "invitations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE identity.invitations SET role_id = role_ids[1];");

            migrationBuilder.DropColumn(
                name: "role_ids",
                schema: "identity",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "user_type",
                schema: "identity",
                table: "invitations");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_role_id",
                schema: "identity",
                table: "invitations",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "fk_invitations_role_id",
                schema: "identity",
                table: "invitations",
                column: "role_id",
                principalSchema: "identity",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
