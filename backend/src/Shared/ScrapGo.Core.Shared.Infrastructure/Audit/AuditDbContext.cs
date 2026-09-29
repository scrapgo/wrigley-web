using Microsoft.EntityFrameworkCore.Design;
using ScrapGo.Core.Shared.Infrastructure.Persistence;

namespace ScrapGo.Core.Shared.Infrastructure.Audit;

/// <summary>
/// Owns the <c>audit</c> schema's migrations. Runtime writes don't go through
/// this context; they go through each module's own DbContext (see
/// <see cref="AuditLogRecorder{TModule, TContext}"/>), so the audit row
/// shares the module's transaction.
/// </summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AuditLogConfiguration.Schema);
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
    }
}

/// <summary>Lets <c>dotnet ef</c> build the context without the host.</summary>
public sealed class AuditDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AuditDbContext>()
            .UseModulePostgres("Host=localhost;Database=scrapgo_design_time", AuditLogConfiguration.Schema)
            .Options);
}
