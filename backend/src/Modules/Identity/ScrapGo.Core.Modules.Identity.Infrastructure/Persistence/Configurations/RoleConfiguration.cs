using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>Seeded id of the built-in <see cref="DefaultRoleNames.OrganizationAdministrator"/> role.</summary>
    public const int OrganizationAdministratorRoleId = 1;

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnType("text")
            .IsRequired();

        // Null for a platform-scoped built-in role.
        builder.Property(r => r.OrganizationId)
            .IsRequired(false);

        builder.Property(r => r.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(RoleStatus.Active);

        builder.Property(r => r.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Ignore(r => r.IsOrganizationScoped);
        builder.Ignore(r => r.IsApplicationRole);

        // Set for application roles (templates and org custom).
        builder.Property(r => r.ApplicationId).IsRequired(false);
        builder.HasIndex(r => r.ApplicationId).HasDatabaseName("ix_roles_application_id");
        builder.HasOne<CatalogApplication>()
            .WithMany()
            .HasForeignKey(r => r.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_roles_application_id");

        // Names are unique per organization. Postgres treats NULLs as distinct
        // in a unique index, so platform-scoped names need their own partial
        // index below.
        builder.HasIndex(r => new { r.OrganizationId, r.Name })
            .IsUnique()
            .HasDatabaseName(IdentityUniqueConstraints.RoleNamePerOrganization);

        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasDatabaseName("ux_roles_name_platform_scope")
            .HasFilter("organization_id IS NULL");

        builder.HasIndex(r => r.OrganizationId)
            .HasDatabaseName("ix_roles_organization_id");

        builder.ToTable(t => t.HasCheckConstraint("ck_roles_status", "status IN ('Active', 'Deleted')"));

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(r => r.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_roles_organization_id");

        builder.HasMany(r => r.RolePermissions)
            .WithOne()
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_role_permissions_role_id");

        // Built-in, platform-scoped roles. Anonymous objects because the
        // entity's setters are private. Granted per organization via UserRole.
        var seededAt = new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(new
        {
            Id = OrganizationAdministratorRoleId,
            Name = DefaultRoleNames.OrganizationAdministrator,
            Description = string.Empty,
            OrganizationId = (int?)null,
            ApplicationId = (int?)null,
            Status = RoleStatus.Active,
            CreatedAt = seededAt,
            UpdatedAt = seededAt,
        });
    }
}
