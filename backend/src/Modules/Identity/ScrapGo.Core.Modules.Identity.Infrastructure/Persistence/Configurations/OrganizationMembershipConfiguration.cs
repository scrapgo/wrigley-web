using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(MembershipStatus.Active);

        builder.Property(m => m.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(m => m.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        // One membership per (user, organization). Status changes in place.
        builder.HasIndex(m => new { m.UserId, m.OrganizationId })
            .IsUnique()
            .HasDatabaseName(IdentityUniqueConstraints.OrganizationMembership);

        builder.HasIndex(m => m.OrganizationId)
            .HasDatabaseName("ix_organization_memberships_organization_id");

        builder.HasIndex(m => m.Status)
            .HasDatabaseName("ix_organization_memberships_status");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_organization_memberships_status",
            "status IN ('Active', 'Disabled')"));

        // RESTRICT everywhere: neither users nor organizations have a hard-delete path.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_organization_memberships_user_id");

        builder.HasOne(m => m.Organization)
            .WithMany()
            .HasForeignKey(m => m.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_organization_memberships_organization_id");
    }
}
