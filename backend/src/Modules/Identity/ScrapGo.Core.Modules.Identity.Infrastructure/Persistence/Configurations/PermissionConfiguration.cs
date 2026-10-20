using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Key)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(p => p.Audience)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnType("text")
            .IsRequired();

        builder.HasIndex(p => p.Key)
            .IsUnique()
            .HasDatabaseName("ux_permissions_key");

        builder.HasIndex(p => p.FeatureId)
            .HasDatabaseName("ix_permissions_feature_id");

        builder.ToTable(t => t.HasCheckConstraint("ck_permissions_audience", "audience IN ('Internal', 'External', 'Any')"));

        builder.HasOne<Feature>()
            .WithMany()
            .HasForeignKey(p => p.FeatureId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_permissions_feature_id");
    }
}
