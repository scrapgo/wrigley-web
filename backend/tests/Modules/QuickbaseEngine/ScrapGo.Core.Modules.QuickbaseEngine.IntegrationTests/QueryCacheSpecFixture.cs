using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.QuickbaseEngine.Application;
using ScrapGo.Core.Modules.QuickbaseEngine.Domain.Caching;
using ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure;
using ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure.Persistence;
using ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure.Quickbase;
using Testcontainers.PostgreSql;

namespace ScrapGo.Core.Modules.QuickbaseEngine.IntegrationTests;

/// <summary>
/// The real <see cref="IQuickbaseQueryService"/>, wired through the module's
/// own DI extensions, running against:
/// <list type="bullet">
/// <item>a real Testcontainers Postgres with the <c>quickbase</c> migrations applied. The upsert is Postgres-specific SQL.</item>
/// <item><see cref="FakeQuickbaseApi"/> behind the real <c>IHttpClientFactory</c> typed client.</item>
/// <item>a <see cref="SettableTimeProvider"/>, so TTL expiry is deterministic.</item>
/// </list>
/// One instance per scenario class. There's no HTTP endpoint yet, so the
/// service is the entry point under test.
/// </summary>
public class QueryCacheSpecFixture : IAsyncLifetime
{
    public const string Realm = "scrapgo-spec.quickbase.com";
    public const string UserToken = "spec-user-token";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private ServiceProvider? _services;

    public FakeQuickbaseApi Quickbase { get; } = new();

    public SettableTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));

    /// <summary>Extra configuration on top of the module's defaults, e.g. a different TTL.</summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> ConfigurationOverrides => [];

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _container.GetConnectionString(),
                ["Quickbase:RealmHostname"] = Realm,
                ["Quickbase:UserToken"] = UserToken,

                // Real retry pipeline, near-zero backoff, so retry scenarios run fast.
                ["Quickbase:RetryBaseDelay"] = "00:00:00.005",
            })
            .AddInMemoryCollection(ConfigurationOverrides)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddQuickbaseEngineApplication().AddQuickbaseEngineInfrastructure(configuration);

        // Same named client the module registers; a fresh handler per factory
        // rotation, all sharing the one fake.
        services.AddHttpClient(QuickbaseHttpClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(Quickbase.CreateHandler);

        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        await using (var scope = _services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<QuickbaseDbContext>().Database.MigrateAsync();
        }

        await ArrangeAsync();
    }

    /// <summary>
    /// Scenario arrangement that must run exactly once per scenario class.
    /// (xUnit runs a test class's own <c>InitializeAsync</c> once per test method.)
    /// </summary>
    protected virtual Task ArrangeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    /// <summary>Runs one query in its own DI scope, the way a request would.</summary>
    public async Task<QuickbaseQueryResult> QueryAsync(QuickbaseQuery query, bool forceRefresh = false)
    {
        await using var scope = _services!.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IQuickbaseQueryService>().QueryAsync(query, forceRefresh);
    }

    public async Task<List<QueryCache>> CacheRowsAsync()
    {
        await using var scope = _services!.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<QuickbaseDbContext>().QueryCaches.AsNoTracking().ToListAsync();
    }

    /// <summary>jsonb normalizes whitespace and key order, so JSON is compared by meaning, not by bytes.</summary>
    public static void AssertSameJson(string expected, string actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), $"Expected {expected} but got {actual}");
}

public sealed class OneMinuteTtlQueryCacheSpecFixture : QueryCacheSpecFixture
{
    protected override IEnumerable<KeyValuePair<string, string?>> ConfigurationOverrides =>
        [new("Quickbase:QueryCache:Ttl", "00:01:00")];
}

public sealed class NoStaleOnErrorQueryCacheSpecFixture : QueryCacheSpecFixture
{
    protected override IEnumerable<KeyValuePair<string, string?>> ConfigurationOverrides =>
        [new("Quickbase:QueryCache:ServeStaleOnError", "false")];
}
