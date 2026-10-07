using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(p => p.ModuleId).IsRequired(false);

        builder.HasIndex(p => p.ModuleId).HasDatabaseName("ix_permissions_module_id");

        builder.HasOne<CatalogModule>()
            .WithMany()
            .HasForeignKey(p => p.ModuleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_permissions_module_id");

        builder.Property(p => p.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("ux_permissions_name");

        // The catalog is a compile-time contract, seeded here and never written
        // over HTTP. Ids are 1-based positions in Permissions.All, so new
        // permissions must be appended, never inserted or reordered.
        var seededAt = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(Permissions.All.Select((name, index) => new
        {
            Id = index + 1,
            Name = name,
            ModuleId = ApplicationCatalog.ModuleIdOf(name),
            CreatedAt = seededAt,
            UpdatedAt = seededAt,
        }));
    }
}
