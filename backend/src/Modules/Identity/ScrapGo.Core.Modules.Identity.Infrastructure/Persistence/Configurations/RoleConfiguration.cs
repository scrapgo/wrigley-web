using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Key)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.Name)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.UserType)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.IsSystem)
            .IsRequired();

        // The target of user_roles' composite foreign key (role_id, user_type).
        builder.HasAlternateKey(r => new { r.Id, r.UserType })
            .HasName("ux_roles_id_user_type");

        builder.HasIndex(r => r.Key)
            .IsUnique()
            .HasDatabaseName("ux_roles_key");

        builder.ToTable(t => t.HasCheckConstraint("ck_roles_user_type", "user_type IN ('Internal', 'External')"));

        builder.HasMany(r => r.RolePermissions)
            .WithOne()
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_role_permissions_role_id");

        // The three system roles (ACCESS-MODEL.md section 2). Anonymous objects
        // because the entity's setters are private.
        builder.HasData(AccessModelSeed.Roles.Select(r => new
        {
            r.Id,
            r.Key,
            r.Name,
            r.Description,
            r.UserType,
            IsSystem = true,
        }));
    }
}
