using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(o => o.Slug)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(o => o.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(OrganizationStatus.Active);

        builder.Property(o => o.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Ignore(o => o.IsActive);

        // The arbiter for "slug already taken": CreateOrganizationHandler maps
        // a violation of this index to 409 rather than pre-checking (racy).
        builder.HasIndex(o => o.Slug)
            .IsUnique()
            .HasDatabaseName(IdentityUniqueConstraints.OrganizationSlug);

        builder.HasIndex(o => o.Status)
            .HasDatabaseName("ix_organizations_status");

        builder.ToTable(t => t.HasCheckConstraint("ck_organizations_status", "status IN ('Active', 'Disabled')"));
    }
}
