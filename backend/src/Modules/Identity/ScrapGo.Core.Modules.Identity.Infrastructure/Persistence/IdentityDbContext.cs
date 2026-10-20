using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ScrapGo.Core.Shared.Infrastructure.Audit;
using ScrapGo.Core.Shared.Infrastructure.Persistence;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// The Identity module's Postgres store, in the <c>identity</c> schema.
/// <c>audit.audit_logs</c> is mapped too but excluded from this context's
/// migrations, so audit rows share this module's transaction while
/// <see cref="AuditDbContext"/> owns the table.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();

    public DbSet<LinkedCredential> LinkedCredentials => Set<LinkedCredential>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <remarks>
    /// Temporary, until B6 resets the migrations: the model has dropped the
    /// application, module, catalog, organization, membership and invitation
    /// tables but the migrations still create them, and EF refuses to migrate
    /// a model that differs from its snapshot.
    /// </remarks>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.MapAuditLogs();
    }
}

/// <summary>Lets <c>dotnet ef</c> build the context without the host.</summary>
public sealed class IdentityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseModulePostgres("Host=localhost;Database=scrapgo_design_time", IdentityDbContext.Schema)
            .Options);
}
