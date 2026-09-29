using Microsoft.Extensions.Configuration;

namespace ScrapGo.Core.Shared.Infrastructure.Persistence;

/// <summary>
/// The one place every module's DbContext is pointed at Postgres, so they
/// all share the same conventions: snake_case identifiers, and a migrations
/// history table inside the module's own schema so each module migrates
/// independently.
/// </summary>
public static class PostgresDbContextOptions
{
    public const string ConnectionStringName = "Default";

    /// <summary>
    /// Fails fast on a missing or blank connection string. <c>UseNpgsql(null)</c>
    /// is silently accepted, so without this the app would start and only
    /// fail later on the first DB-touching request. The message deliberately
    /// omits the value, because connection strings must never be logged.
    /// </summary>
    public static string GetRequiredConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is not configured (set ConnectionStrings__{ConnectionStringName}).")
            : connectionString;
    }

    public static TBuilder UseModulePostgres<TBuilder>(this TBuilder options, string connectionString, string schema)
        where TBuilder : DbContextOptionsBuilder
    {
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention();

        return options;
    }
}
