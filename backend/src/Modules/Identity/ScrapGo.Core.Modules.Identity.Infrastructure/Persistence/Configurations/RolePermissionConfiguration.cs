using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");

        // Compound key: a permission is granted to a role at most once.
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.HasIndex(rp => rp.PermissionId)
            .HasDatabaseName("ix_role_permissions_permission_id");

        // The role side of this relationship is configured in RoleConfiguration.
        builder.HasOne(rp => rp.Permission)
            .WithMany()
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_role_permissions_permission_id");
    }
}
