using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnType("text")
            .IsRequired();

        // Only to filter out the old schema's organization custom roles until B6.
        builder.Property<int?>(LegacyScope.Property).HasColumnName(LegacyScope.Column);

        builder.Property(r => r.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(RoleStatus.Active);

        builder.Property(r => r.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        // Role names are unique. The name is the old schema's platform-scope
        // index, the one that applies now that roles are global.
        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasDatabaseName("ux_roles_name_platform_scope");

        builder.ToTable(t => t.HasCheckConstraint("ck_roles_status", "status IN ('Active', 'Deleted')"));

        builder.HasMany(r => r.RolePermissions)
            .WithOne()
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_role_permissions_role_id");
    }
}
