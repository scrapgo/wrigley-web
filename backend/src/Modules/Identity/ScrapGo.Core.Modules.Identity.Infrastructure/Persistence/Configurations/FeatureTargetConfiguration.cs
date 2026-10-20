using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class FeatureTargetConfiguration : IEntityTypeConfiguration<FeatureTarget>
{
    public void Configure(EntityTypeBuilder<FeatureTarget> builder)
    {
        builder.ToTable("feature_targets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired(false);

        builder.Property(t => t.RoleId).IsRequired(false);

        // One row per target. NULLS NOT DISTINCT, because exactly one of
        // user_id / role_id is always null and plain NULLs never collide.
        builder.HasIndex(t => new { t.FeatureId, t.UserId, t.RoleId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_feature_targets_feature_id_user_id_role_id");

        builder.HasIndex(t => t.UserId).HasDatabaseName("ix_feature_targets_user_id");

        builder.HasIndex(t => t.RoleId).HasDatabaseName("ix_feature_targets_role_id");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_feature_targets_exactly_one_target", "(user_id IS NULL) <> (role_id IS NULL)"));

        builder.HasOne<Feature>()
            .WithMany()
            .HasForeignKey(t => t.FeatureId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_feature_targets_feature_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_feature_targets_user_id");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(t => t.RoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_feature_targets_role_id");
    }
}
