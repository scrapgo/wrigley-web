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

        builder.Property(ur => ur.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(ur => ur.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(ur => ur.UserId).HasDatabaseName("ix_user_roles_user_id");
        builder.HasIndex(ur => ur.RoleId).HasDatabaseName("ix_user_roles_role_id");
        builder.HasIndex(ur => ur.OrganizationId).HasDatabaseName("ix_user_roles_organization_id");

        // A role is held at most once per scope. The partial index covers the
        // platform scope, since NULL organization ids never collide in the
        // composite one.
        builder.HasIndex(ur => new { ur.UserId, ur.RoleId, ur.OrganizationId })
            .IsUnique()
            .HasDatabaseName("ux_user_roles_user_id_role_id_organization_id");

        builder.HasIndex(ur => new { ur.UserId, ur.RoleId })
            .IsUnique()
            .HasDatabaseName("ux_user_roles_user_id_role_id_platform_scope")
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

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(ur => ur.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_roles_organization_id");
    }
}
