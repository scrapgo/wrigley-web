using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class LinkedCredentialConfiguration : IEntityTypeConfiguration<LinkedCredential>
{
    public void Configure(EntityTypeBuilder<LinkedCredential> builder)
    {
        builder.ToTable("linked_credentials");

        builder.HasKey(lc => lc.Id);

        // Nullable, with no FK yet: the organization identity providers it
        // points at belong to the SSO module, which hasn't been migrated.
        builder.Property(lc => lc.IdentityProviderId)
            .IsRequired(false);

        builder.Property(lc => lc.ProviderName)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(lc => lc.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(LinkedCredentialStatus.Active);

        builder.Property(lc => lc.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(lc => lc.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        // One row per (user, provider): linking upserts rather than accumulating.
        builder.HasIndex(lc => new { lc.UserId, lc.ProviderName })
            .IsUnique()
            .HasDatabaseName("ux_linked_credentials_user_id_provider_name");

        builder.HasIndex(lc => lc.IdentityProviderId)
            .HasDatabaseName("ix_linked_credentials_identity_provider_id");

        builder.HasIndex(lc => lc.Status)
            .HasDatabaseName("ix_linked_credentials_status");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_linked_credentials_status",
            "status IN ('Active', 'Orphaned')"));

        // RESTRICT: users have no hard-delete path.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(lc => lc.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_linked_credentials_user_id");
    }
}
