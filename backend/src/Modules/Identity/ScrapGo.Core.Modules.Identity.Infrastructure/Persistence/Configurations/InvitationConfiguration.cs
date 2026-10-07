using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations", t =>
            t.HasCheckConstraint("ck_invitations_status", "status IN ('Pending', 'Accepted', 'Revoked')"));

        builder.HasKey(i => i.Id);

        builder.Property(i => i.EmailNormalized).HasColumnType("text").IsRequired();
        builder.Property(i => i.TokenHash).HasColumnType("text").IsRequired();
        builder.Property(i => i.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(InvitationStatus.Pending);
        builder.Property(i => i.ExpiresAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(i => i.AcceptedAt).HasColumnType("timestamptz");
        builder.Property(i => i.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(i => i.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(i => i.TokenHash).IsUnique().HasDatabaseName("ux_invitations_token_hash");

        // At most one pending invitation per email per organization.
        builder.HasIndex(i => new { i.OrganizationId, i.EmailNormalized })
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName(IdentityUniqueConstraints.PendingInvitation);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(i => i.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_invitations_organization_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_invitations_invited_by_user_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_invitations_accepted_by_user_id");

        builder.HasMany(i => i.Grants)
            .WithOne()
            .HasForeignKey(g => g.InvitationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_invitation_grants_invitation_id");

        builder.Navigation(i => i.Grants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class InvitationGrantConfiguration : IEntityTypeConfiguration<InvitationGrant>
{
    public void Configure(EntityTypeBuilder<InvitationGrant> builder)
    {
        builder.ToTable("invitation_grants");

        builder.HasKey(g => g.Id);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(g => g.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_invitation_grants_role_id");

        builder.HasOne<CatalogApplication>()
            .WithMany()
            .HasForeignKey(g => g.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_invitation_grants_application_id");
    }
}
