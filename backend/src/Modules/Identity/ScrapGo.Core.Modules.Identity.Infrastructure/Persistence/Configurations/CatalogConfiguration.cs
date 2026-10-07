using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>The global application catalog, seeded from <see cref="ApplicationCatalog"/> (empty today: blank slate).</summary>
public sealed class CatalogApplicationConfiguration : IEntityTypeConfiguration<CatalogApplication>
{
    public void Configure(EntityTypeBuilder<CatalogApplication> builder)
    {
        builder.ToTable("applications", t => t.HasCheckConstraint("ck_applications_status", "status IN ('Active', 'Retired')"));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Key).HasColumnType("text").IsRequired();
        builder.Property(a => a.Name).HasColumnType("text").IsRequired();
        builder.Property(a => a.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(CatalogStatus.Active);
        builder.Property(a => a.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Ignore(a => a.IsActive);

        builder.HasIndex(a => a.Key).IsUnique().HasDatabaseName("ux_applications_key");

        builder.HasData(ApplicationCatalog.All.Select(a => new
        {
            a.Id,
            a.Key,
            a.Name,
            Status = CatalogStatus.Active,
            CreatedAt = CatalogSeed.SeededAt,
            UpdatedAt = CatalogSeed.SeededAt,
        }));
    }
}

public sealed class CatalogModuleConfiguration : IEntityTypeConfiguration<CatalogModule>
{
    public void Configure(EntityTypeBuilder<CatalogModule> builder)
    {
        builder.ToTable("modules", t => t.HasCheckConstraint("ck_modules_status", "status IN ('Active', 'Retired')"));

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Key).HasColumnType("text").IsRequired();
        builder.Property(m => m.Name).HasColumnType("text").IsRequired();
        builder.Property(m => m.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(CatalogStatus.Active);
        builder.Property(m => m.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Ignore(m => m.IsActive);

        builder.HasIndex(m => new { m.ApplicationId, m.Key }).IsUnique().HasDatabaseName("ux_modules_application_id_key");

        // Target of the composite FK that proves an enabled module belongs to
        // the organization application's own application.
        builder.HasAlternateKey(m => new { m.ApplicationId, m.Id }).HasName("ak_modules_application_id_id");

        builder.HasOne<CatalogApplication>()
            .WithMany()
            .HasForeignKey(m => m.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_modules_application_id");

        builder.HasData(ApplicationCatalog.All.SelectMany(a => a.Modules.Select(m => new
        {
            m.Id,
            ApplicationId = a.Id,
            m.Key,
            m.Name,
            Status = CatalogStatus.Active,
            CreatedAt = CatalogSeed.SeededAt,
            UpdatedAt = CatalogSeed.SeededAt,
        })));
    }
}

public sealed class OrganizationApplicationConfiguration : IEntityTypeConfiguration<OrganizationApplication>
{
    public void Configure(EntityTypeBuilder<OrganizationApplication> builder)
    {
        builder.ToTable("organization_applications", t =>
            t.HasCheckConstraint("ck_organization_applications_status", "status IN ('Active', 'Removed')"));

        builder.HasKey(oa => oa.Id);

        builder.Property(oa => oa.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(OrganizationApplicationStatus.Active);
        builder.Property(oa => oa.EnabledAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(oa => oa.RemovedAt).HasColumnType("timestamptz");
        builder.Property(oa => oa.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(oa => oa.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Ignore(oa => oa.IsActive);

        builder.HasIndex(oa => new { oa.OrganizationId, oa.ApplicationId })
            .IsUnique()
            .HasDatabaseName(IdentityUniqueConstraints.OrganizationApplication);
        builder.HasIndex(oa => oa.ApplicationId).HasDatabaseName("ix_organization_applications_application_id");

        builder.HasAlternateKey(oa => new { oa.Id, oa.ApplicationId }).HasName("ak_organization_applications_id_application_id");

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(oa => oa.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_organization_applications_organization_id");

        builder.HasOne<CatalogApplication>()
            .WithMany()
            .HasForeignKey(oa => oa.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_organization_applications_application_id");
    }
}

public sealed class OrganizationApplicationModuleConfiguration : IEntityTypeConfiguration<OrganizationApplicationModule>
{
    public void Configure(EntityTypeBuilder<OrganizationApplicationModule> builder)
    {
        builder.ToTable("organization_application_modules", t =>
            t.HasCheckConstraint("ck_organization_application_modules_status", "status IN ('Enabled', 'Disabled')"));

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(ModuleEnablementStatus.Enabled);
        builder.Property(m => m.EnabledAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(m => m.DisabledAt).HasColumnType("timestamptz");
        builder.Property(m => m.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Ignore(m => m.IsEnabled);

        builder.HasIndex(m => new { m.OrganizationApplicationId, m.ModuleId })
            .IsUnique()
            .HasDatabaseName(IdentityUniqueConstraints.OrganizationApplicationModule);
        builder.HasIndex(m => new { m.ApplicationId, m.ModuleId }).HasDatabaseName("ix_organization_application_modules_application_id_module_id");

        // Both composite: the row's application is the organization
        // application's, and the module is that application's. A module of
        // another application can't be enabled here, at the database level.
        builder.HasOne<OrganizationApplication>()
            .WithMany()
            .HasForeignKey(m => new { m.OrganizationApplicationId, m.ApplicationId })
            .HasPrincipalKey(oa => new { oa.Id, oa.ApplicationId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_organization_application_modules_organization_application");

        builder.HasOne<CatalogModule>()
            .WithMany()
            .HasForeignKey(m => new { m.ApplicationId, m.ModuleId })
            .HasPrincipalKey(md => new { md.ApplicationId, md.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_organization_application_modules_module");
    }
}

internal static class CatalogSeed
{
    public static readonly DateTimeOffset SeededAt = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
}
