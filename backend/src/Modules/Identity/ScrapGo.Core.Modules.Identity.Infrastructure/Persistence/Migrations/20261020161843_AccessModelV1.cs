using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Moves the Identity schema to the access model (ACCESS-MODEL.md, B6-alt):
    /// drops the old organization / application / role tables, keeps users
    /// (classification becomes user_type), creates the new tables with their seed,
    /// and gives existing users the new system roles. Forward-only.
    /// </summary>
    public partial class AccessModelV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Remember who held the old PlatformAdministrator role at platform
            //    scope, before its tables go, so they become site_admin.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE access_model_v1_site_admins AS
                SELECT DISTINCT ur.user_id
                FROM identity.user_roles ur
                JOIN identity.roles r ON r.id = ur.role_id
                WHERE r.name = 'PlatformAdministrator' AND ur.organization_id IS NULL;
                """);

            // 2. Drop every table of the old organization / application / role
            //    model. Users are kept.
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS
                    identity.invitation_grants,
                    identity.invitations,
                    identity.organization_application_modules,
                    identity.organization_applications,
                    identity.modules,
                    identity.applications,
                    identity.linked_credentials,
                    identity.organization_memberships,
                    identity.user_roles,
                    identity.role_permissions,
                    identity.permissions,
                    identity.roles,
                    identity.organizations
                CASCADE;
                """);

            // 3. users: classification becomes user_type (same values, data kept),
            //    plus the new profile columns and the (id, user_type) key the
            //    composite foreign keys point at.
            migrationBuilder.DropIndex(
                name: "ix_users_classification",
                schema: "identity",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_classification",
                schema: "identity",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "classification",
                schema: "identity",
                table: "users",
                newName: "user_type");

            migrationBuilder.Sql("ALTER TABLE identity.users ALTER COLUMN user_type DROP DEFAULT;");

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sign_in_provider",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_sign_in_at",
                schema: "identity",
                table: "users",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_user_type",
                schema: "identity",
                table: "users",
                sql: "user_type IN ('Internal', 'External')");

            migrationBuilder.AddUniqueConstraint(
                name: "ux_users_id_user_type",
                schema: "identity",
                table: "users",
                columns: new[] { "id", "user_type" });

            migrationBuilder.CreateIndex(
                name: "ix_users_user_type",
                schema: "identity",
                table: "users",
                column: "user_type");

            // 4. The new tables, the seed (ACCESS-MODEL.md section 6) and indexes.
            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    user_type = table.Column<string>(type: "text", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                    table.UniqueConstraint("ux_roles_id_user_type", x => new { x.id, x.user_type });
                    table.CheckConstraint("ck_roles_user_type", "user_type IN ('Internal', 'External')");
                });

            migrationBuilder.CreateTable(
                name: "features",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    is_toggleable = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_features", x => x.id);
                    table.CheckConstraint("ck_features_locked_is_on", "is_toggleable OR state = 'On'");
                    table.CheckConstraint("ck_features_state", "state IN ('Off', 'On', 'Targeted')");
                    table.ForeignKey(
                        name: "fk_features_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "text", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    supplier_record_ids = table.Column<List<int>>(type: "integer[]", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    invited_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    accepted_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitations", x => x.id);
                    table.CheckConstraint("ck_invitations_status", "status IN ('Pending', 'Accepted', 'Revoked', 'Expired')");
                    table.ForeignKey(
                        name: "fk_invitations_accepted_by_user_id",
                        column: x => x.accepted_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_invitations_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invitations_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    user_type = table.Column<string>(type: "text", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    assigned_by_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.CheckConstraint("ck_user_roles_user_type", "user_type IN ('Internal', 'External')");
                    table.ForeignKey(
                        name: "fk_user_roles_assigned_by_user_id",
                        column: x => x.assigned_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_roles_role_id_user_type",
                        columns: x => new { x.role_id, x.user_type },
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumns: new[] { "id", "user_type" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_roles_user_id_user_type",
                        columns: x => new { x.user_id, x.user_type },
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumns: new[] { "id", "user_type" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_suppliers",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    supplier_record_id = table.Column<int>(type: "integer", nullable: false),
                    user_type = table.Column<string>(type: "text", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    linked_by_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_suppliers", x => new { x.user_id, x.supplier_record_id });
                    table.CheckConstraint("ck_user_suppliers_external", "user_type = 'External'");
                    table.ForeignKey(
                        name: "fk_user_suppliers_linked_by_user_id",
                        column: x => x.linked_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_suppliers_user_id_user_type",
                        columns: x => new { x.user_id, x.user_type },
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumns: new[] { "id", "user_type" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feature_targets",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    feature_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    role_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature_targets", x => x.id);
                    table.CheckConstraint("ck_feature_targets_exactly_one_target", "(user_id IS NULL) <> (role_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_feature_targets_feature_id",
                        column: x => x.feature_id,
                        principalSchema: "identity",
                        principalTable: "features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_feature_targets_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_feature_targets_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "text", nullable: false),
                    feature_id = table.Column<int>(type: "integer", nullable: false),
                    audience = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.id);
                    table.CheckConstraint("ck_permissions_audience", "audience IN ('Internal', 'External', 'Any')");
                    table.ForeignKey(
                        name: "fk_permissions_feature_id",
                        column: x => x.feature_id,
                        principalSchema: "identity",
                        principalTable: "features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "identity",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    permission_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_role_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "identity",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_permissions_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "features",
                columns: new[] { "id", "description", "is_toggleable", "key", "name", "state", "updated_at", "updated_by_user_id" },
                values: new object[,]
                {
                    { 1, "", true, "dashboard", "Dashboard", "On", new DateTimeOffset(new DateTime(2026, 10, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2, "", true, "suppliers", "Suppliers", "On", new DateTimeOffset(new DateTime(2026, 10, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3, "", true, "supplier_edit", "Supplier editing", "Targeted", new DateTimeOffset(new DateTime(2026, 10, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4, "", false, "users", "Users", "On", new DateTimeOffset(new DateTime(2026, 10, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 5, "", false, "roles", "Roles", "On", new DateTimeOffset(new DateTime(2026, 10, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6, "", false, "features", "Features", "On", new DateTimeOffset(new DateTime(2026, 10, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 7, "", false, "audit", "Audit", "On", new DateTimeOffset(new DateTime(2026, 10, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "roles",
                columns: new[] { "id", "description", "is_system", "key", "name", "user_type" },
                values: new object[,]
                {
                    { 1, "A few ScrapGo staff with full administration.", true, "site_admin", "Site Admin", "Internal" },
                    { 2, "Every internal user, assigned on first sign-in.", true, "scrapgo_user", "ScrapGo User", "Internal" },
                    { 3, "Buyers, sellers and transporters. Joins by invitation only.", true, "user", "User", "External" }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "permissions",
                columns: new[] { "id", "audience", "description", "feature_id", "key", "name" },
                values: new object[,]
                {
                    { 1, "Any", "", 1, "dashboard.view", "View the dashboard" },
                    { 2, "Internal", "", 2, "suppliers.view_all", "View all suppliers" },
                    { 3, "External", "", 2, "suppliers.view_own", "View linked suppliers" },
                    { 4, "Internal", "", 3, "suppliers.edit_all", "Edit all suppliers" },
                    { 5, "External", "", 3, "suppliers.edit_own", "Edit linked suppliers" },
                    { 6, "Internal", "", 4, "users.view", "View users" },
                    { 7, "Internal", "", 4, "users.invite", "Invite users" },
                    { 8, "Internal", "", 4, "users.manage", "Manage users" },
                    { 9, "Internal", "", 5, "roles.view", "View roles" },
                    { 10, "Internal", "", 5, "roles.manage", "Manage roles" },
                    { 11, "Internal", "", 6, "features.view", "View features" },
                    { 12, "Internal", "", 6, "features.manage", "Manage features" },
                    { 13, "Internal", "", 7, "audit.view", "View the audit log" }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 4, 1 },
                    { 6, 1 },
                    { 7, 1 },
                    { 8, 1 },
                    { 9, 1 },
                    { 10, 1 },
                    { 11, 1 },
                    { 12, 1 },
                    { 13, 1 },
                    { 1, 2 },
                    { 2, 2 },
                    { 4, 2 },
                    { 7, 2 },
                    { 1, 3 },
                    { 3, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_feature_targets_role_id",
                schema: "identity",
                table: "feature_targets",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_feature_targets_user_id",
                schema: "identity",
                table: "feature_targets",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_feature_targets_feature_id_user_id_role_id",
                schema: "identity",
                table: "feature_targets",
                columns: new[] { "feature_id", "user_id", "role_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_features_updated_by_user_id",
                schema: "identity",
                table: "features",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_features_key",
                schema: "identity",
                table: "features",
                column: "key",
                unique: true);

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
                name: "ix_invitations_role_id",
                schema: "identity",
                table: "invitations",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ux_invitations_email_pending",
                schema: "identity",
                table: "invitations",
                column: "email",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ux_invitations_token_hash",
                schema: "identity",
                table: "invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_permissions_feature_id",
                schema: "identity",
                table: "permissions",
                column: "feature_id");

            migrationBuilder.CreateIndex(
                name: "ux_permissions_key",
                schema: "identity",
                table: "permissions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_id",
                schema: "identity",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ux_roles_key",
                schema: "identity",
                table: "roles",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_assigned_by_user_id",
                schema: "identity",
                table: "user_roles",
                column: "assigned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                schema: "identity",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id_user_type",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "role_id", "user_type" });

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_user_id_user_type",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "user_id", "user_type" });

            migrationBuilder.CreateIndex(
                name: "ix_user_suppliers_linked_by_user_id",
                schema: "identity",
                table: "user_suppliers",
                column: "linked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_suppliers_supplier_record_id",
                schema: "identity",
                table: "user_suppliers",
                column: "supplier_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_suppliers_user_id_user_type",
                schema: "identity",
                table: "user_suppliers",
                columns: new[] { "user_id", "user_type" });

            // 5. Seeded rows have explicit ids; move each identity sequence past them.
            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('identity.roles', 'id'), (SELECT max(id) FROM identity.roles));
                SELECT setval(pg_get_serial_sequence('identity.features', 'id'), (SELECT max(id) FROM identity.features));
                SELECT setval(pg_get_serial_sequence('identity.permissions', 'id'), (SELECT max(id) FROM identity.permissions));
                """);

            // 6. Existing users get the new system roles: every internal user is a
            //    ScrapGo User, former platform administrators are also Site Admins,
            //    and every external user is a User (with no supplier links yet).
            migrationBuilder.Sql("""
                INSERT INTO identity.user_roles (user_id, role_id, user_type, assigned_at)
                SELECT u.id, r.id, u.user_type, now()
                FROM identity.users u
                JOIN identity.roles r ON r.key = 'scrapgo_user'
                WHERE u.user_type = 'Internal';

                INSERT INTO identity.user_roles (user_id, role_id, user_type, assigned_at)
                SELECT u.id, r.id, u.user_type, now()
                FROM identity.users u
                JOIN access_model_v1_site_admins s ON s.user_id = u.id
                JOIN identity.roles r ON r.key = 'site_admin'
                WHERE u.user_type = 'Internal';

                INSERT INTO identity.user_roles (user_id, role_id, user_type, assigned_at)
                SELECT u.id, r.id, u.user_type, now()
                FROM identity.users u
                JOIN identity.roles r ON r.key = 'user'
                WHERE u.user_type = 'External';

                DROP TABLE access_model_v1_site_admins;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(
                "AccessModelV1 is forward-only: the old organization and role tables can't be rebuilt from the new model.");
    }
}
