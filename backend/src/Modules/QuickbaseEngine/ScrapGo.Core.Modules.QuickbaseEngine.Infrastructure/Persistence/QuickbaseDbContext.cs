using Microsoft.EntityFrameworkCore.Design;
using ScrapGo.Core.Shared.Infrastructure.Persistence;

namespace ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure.Persistence;

/// <summary>
/// The QuickbaseEngine module's store, in the <c>quickbase</c> schema. It
/// holds only this app's own state about Quickbase, such as the query cache.
/// Quickbase remains the system of record for the business data itself.
/// </summary>
public sealed class QuickbaseDbContext(DbContextOptions<QuickbaseDbContext> options) : DbContext(options)
{
    public const string Schema = "quickbase";

    public DbSet<QueryCache> QueryCaches => Set<QueryCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuickbaseDbContext).Assembly);
    }
}

/// <summary>Lets <c>dotnet ef</c> build the context without the host.</summary>
public sealed class QuickbaseDbContextDesignTimeFactory : IDesignTimeDbContextFactory<QuickbaseDbContext>
{
    public QuickbaseDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<QuickbaseDbContext>()
            .UseModulePostgres("Host=localhost;Database=scrapgo_design_time", QuickbaseDbContext.Schema)
            .Options);
}
