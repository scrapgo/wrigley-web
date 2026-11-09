using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class MfaChallengeConfiguration : IEntityTypeConfiguration<MfaChallenge>
{
    public void Configure(EntityTypeBuilder<MfaChallenge> builder)
    {
        builder.ToTable("mfa_challenges");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.AuthTime)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.CodeSalt)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.CodeHash)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.ExpiresAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.LastSentAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.VerifiedAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(c => c.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Ignore(c => c.IsVerified);

        // One challenge per sign-in.
        builder.HasIndex(c => new { c.UserId, c.AuthTime })
            .IsUnique()
            .HasDatabaseName(IdentityUniqueConstraints.MfaChallengeSignIn);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_mfa_challenges_attempts", "attempts >= 0");
            t.HasCheckConstraint("ck_mfa_challenges_send_count", "send_count >= 1");
        });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_mfa_challenges_user_id");
    }
}
