using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure.Persistence;
using ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure.Quickbase;
using ScrapGo.Core.Shared.Infrastructure.Persistence;

namespace ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure;

public static class QuickbaseEngineInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddQuickbaseEngineInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = PostgresDbContextOptions.GetRequiredConnectionString(configuration);

        services.AddDbContext<QuickbaseDbContext>(options =>
            options.UseModulePostgres(connectionString, QuickbaseDbContext.Schema));

        services.AddScoped<IQueryCacheStore, QueryCacheStore>();

        // The cache settings have safe defaults, so they are checked at startup.
        services.AddOptions<QuickbaseQueryCacheOptions>()
            .Bind(configuration.GetSection(QuickbaseQueryCacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The credentials are checked on first use instead (see QuickbaseOptions),
        // so a host or test that never calls Quickbase starts without them.
        services.AddOptions<QuickbaseOptions>()
            .Bind(configuration.GetSection(QuickbaseOptions.SectionName))
            .ValidateDataAnnotations();

        // Same section, but no secrets and safe defaults, so checked at startup
        // (the resilience pipeline reads them at startup anyway).
        services.AddOptions<QuickbaseResilienceOptions>()
            .Bind(configuration.GetSection(QuickbaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddHttpClient<IQuickbaseClient, QuickbaseHttpClient>(QuickbaseHttpClient.HttpClientName, (sp, http) =>
            {
                var options = sp.GetRequiredService<IOptions<QuickbaseOptions>>().Value;

                http.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");

                // Timeouts belong to the resilience pipeline below (per attempt
                // and in total). An HttpClient timeout would cut across retries.
                http.Timeout = Timeout.InfiniteTimeSpan;

                http.DefaultRequestHeaders.Add("QB-Realm-Hostname", options.RealmHostname);
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("QB-USER-TOKEN", options.UserToken);
                http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResilienceHandler()
            .Configure((resilience, sp) => ConfigureResilience(resilience, sp.GetRequiredService<IOptions<QuickbaseResilienceOptions>>().Value));

        return services;
    }

    /// <summary>
    /// Polly v8 via <c>Microsoft.Extensions.Http.Resilience</c>'s standard
    /// pipeline: a total timeout, then retries with exponential backoff and
    /// jitter, then a circuit breaker, then a per-attempt timeout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Retries apply to POST as well. That is safe only because every call on
    /// this client is a read (<c>records/query</c>). Before adding a
    /// non-idempotent Quickbase call (record upserts, deletes), give it a
    /// client or pipeline with
    /// <c>Retry.DisableForUnsafeHttpMethods()</c>, or a retried write could
    /// apply twice.
    /// </para>
    /// <para>
    /// A 429 honors Quickbase's <c>Retry-After</c>. Once retries are exhausted,
    /// or while the circuit is open, the call fails fast and
    /// <see cref="QuickbaseQueryService"/>'s stale-if-error takes over.
    /// </para>
    /// </remarks>
    private static void ConfigureResilience(HttpStandardResilienceOptions resilience, QuickbaseResilienceOptions quickbase)
    {
        resilience.Retry.MaxRetryAttempts = quickbase.MaxRetryAttempts;
        resilience.Retry.Delay = quickbase.RetryBaseDelay;
        resilience.Retry.BackoffType = DelayBackoffType.Exponential;
        resilience.Retry.UseJitter = true;

        resilience.AttemptTimeout.Timeout = quickbase.Timeout;

        // Covers every attempt plus the worst-case backoff (delays are capped at 30s each).
        resilience.TotalRequestTimeout.Timeout =
            (quickbase.Timeout * (quickbase.MaxRetryAttempts + 1)) + (TimeSpan.FromSeconds(30) * quickbase.MaxRetryAttempts);

        // The breaker's sampling window must be at least twice the attempt timeout.
        resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromTicks(
            Math.Max(resilience.CircuitBreaker.SamplingDuration.Ticks, (quickbase.Timeout * 2).Ticks));
    }
}
