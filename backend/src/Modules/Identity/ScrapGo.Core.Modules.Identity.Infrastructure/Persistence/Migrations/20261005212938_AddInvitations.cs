using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invitations",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    organization_id = table.Column<int>(type: "integer", nullable: false),
                    email_normalized = table.Column<string>(type: "text", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    invited_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    accepted_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitations", x => x.id);
                    table.CheckConstraint("ck_invitations_status", "status IN ('Pending', 'Accepted', 'Revoked')");
                    table.ForeignKey(
                        name: "fk_invitations_accepted_by_user_id",
                        column: x => x.accepted_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invitations_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invitations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invitation_grants",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invitation_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    application_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitation_grants", x => x.id);
                    table.ForeignKey(
                        name: "fk_invitation_grants_application_id",
                        column: x => x.application_id,
                        principalSchema: "identity",
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invitation_grants_invitation_id",
                        column: x => x.invitation_id,
                        principalSchema: "identity",
                        principalTable: "invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invitation_grants_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invitation_grants_application_id",
                schema: "identity",
                table: "invitation_grants",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitation_grants_invitation_id",
                schema: "identity",
                table: "invitation_grants",
                column: "invitation_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitation_grants_role_id",
                schema: "identity",
                table: "invitation_grants",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_accepted_by_user_id",
                schema: "identity",
                table: "invitations",
                column: "accepted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_invited_by_user_id",
                schema: "identity",
                table: "invitations",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_invitations_organization_id_email_pending",
                schema: "identity",
                table: "invitations",
                columns: new[] { "organization_id", "email_normalized" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ux_invitations_token_hash",
                schema: "identity",
                table: "invitations",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitation_grants",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "invitations",
                schema: "identity");
        }
    }
}
