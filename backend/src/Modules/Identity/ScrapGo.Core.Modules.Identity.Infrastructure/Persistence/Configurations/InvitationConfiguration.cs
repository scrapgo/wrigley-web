using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Email)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(i => i.SupplierRecordIds)
            .HasColumnType("integer[]")
            .IsRequired();

        builder.Property(i => i.TokenHash)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(i => i.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(InvitationStatus.Pending);

        builder.Property(i => i.ExpiresAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(i => i.AcceptedAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(i => i.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(i => i.TokenHash)
            .IsUnique()
            .HasDatabaseName(IdentityUniqueConstraints.InvitationTokenHash);

        // At most one pending invitation per email.
        builder.HasIndex(i => i.Email)
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName(IdentityUniqueConstraints.PendingInvitationEmail);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_invitations_status", "status IN ('Pending', 'Accepted', 'Revoked', 'Expired')"));

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(i => i.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_invitations_role_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_invitations_invited_by_user_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.AcceptedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_invitations_accepted_by_user_id");
    }
}
