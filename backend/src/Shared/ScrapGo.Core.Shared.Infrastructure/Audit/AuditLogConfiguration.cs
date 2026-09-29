using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Shared.Infrastructure.Audit;

/// <summary>
/// Mapping for <c>audit.audit_logs</c>. Applied by <see cref="AuditDbContext"/>
/// (which owns the table's migrations) and by every module DbContext via
/// <see cref="ModelBuilderAuditExtensions.MapAuditLogs"/> (excluded from
/// that module's migrations), so a module can stage audit rows in its own
/// transaction.
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public const string Schema = "audit";

    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", Schema);

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EventType)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(a => a.ActorType)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(AuditActorType.Human);

        builder.Property(a => a.Metadata)
            .HasColumnType("jsonb");

        builder.Property(a => a.EventTime)
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(a => a.Exported)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(a => a.OrganizationId).HasDatabaseName("ix_audit_logs_organization_id");
        builder.HasIndex(a => a.UserId).HasDatabaseName("ix_audit_logs_user_id");
        builder.HasIndex(a => a.EventTime).HasDatabaseName("ix_audit_logs_event_time");
        builder.HasIndex(a => a.EventType).HasDatabaseName("ix_audit_logs_event_type");
        builder.HasIndex(a => a.ServicePrincipalId).HasDatabaseName("ix_audit_logs_service_principal_id");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_audit_logs_actor_type",
            "actor_type IN ('Human', 'Service', 'System')"));
    }
}

public static class ModelBuilderAuditExtensions
{
    /// <summary>
    /// Maps <c>audit.audit_logs</c> into a module's model without adding it to
    /// that module's migrations. <see cref="AuditDbContext"/> owns the table.
    /// </summary>
    public static ModelBuilder MapAuditLogs(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.Entity<AuditLog>().ToTable("audit_logs", AuditLogConfiguration.Schema, t => t.ExcludeFromMigrations());

        return modelBuilder;
    }
}
