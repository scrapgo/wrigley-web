using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.ToTable("features");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Key)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(f => f.Name)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(f => f.Description)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(f => f.State)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(f => f.IsToggleable)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(f => f.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(f => f.UpdatedByUserId)
            .IsRequired(false);

        builder.HasIndex(f => f.Key)
            .IsUnique()
            .HasDatabaseName("ux_features_key");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_features_state", "state IN ('Off', 'On', 'Targeted')");

            // Locked features (the admin ones) are always On.
            t.HasCheckConstraint("ck_features_locked_is_on", "is_toggleable OR state = 'On'");
        });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_features_updated_by_user_id");
    }
}
