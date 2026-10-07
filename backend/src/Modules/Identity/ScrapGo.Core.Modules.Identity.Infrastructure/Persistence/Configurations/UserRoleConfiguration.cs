using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        builder.HasKey(ur => ur.Id);

        // Null for a platform-scoped assignment.
        builder.Property(ur => ur.OrganizationId)
            .IsRequired(false);

        // Set for an application grant; always the granted role's own
        // application (enforced by the granting service).
        builder.Property(ur => ur.ApplicationId).IsRequired(false);

        builder.Property(ur => ur.ExpiresAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(ur => ur.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(ur => ur.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(ur => ur.UserId).HasDatabaseName("ix_user_roles_user_id");
        builder.HasIndex(ur => ur.RoleId).HasDatabaseName("ix_user_roles_role_id");
        builder.HasIndex(ur => ur.OrganizationId).HasDatabaseName("ix_user_roles_organization_id");
        builder.HasIndex(ur => new { ur.OrganizationId, ur.ApplicationId }).HasDatabaseName("ix_user_roles_organization_id_application_id");

        // No platform-wide application grants: an application grant is always
        // inside one organization.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_user_roles_application_scope", "application_id IS NULL OR organization_id IS NOT NULL"));

        // A role is held at most once per scope. The partial index covers the
        // platform scope, since NULL organization ids never collide in the
        // composite one.
        builder.HasIndex(ur => new { ur.UserId, ur.RoleId, ur.OrganizationId })
            .IsUnique()
            .HasDatabaseName(IdentityUniqueConstraints.OrganizationRoleAssignment);

        builder.HasIndex(ur => new { ur.UserId, ur.RoleId })
            .IsUnique()
            .HasDatabaseName(IdentityUniqueConstraints.PlatformRoleAssignment)
            .HasFilter("organization_id IS NULL");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_roles_user_id");

        builder.HasOne(ur => ur.Role)
            .WithMany()
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_roles_role_id");

        builder.HasOne<CatalogApplication>()
            .WithMany()
            .HasForeignKey(ur => ur.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_roles_application_id");

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(ur => ur.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_roles_organization_id");
    }
}
