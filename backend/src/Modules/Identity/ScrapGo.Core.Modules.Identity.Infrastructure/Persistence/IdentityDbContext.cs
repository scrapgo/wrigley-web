using Microsoft.EntityFrameworkCore.Design;
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

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

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
