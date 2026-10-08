using System.Collections.Concurrent;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Fixtures;

/// <summary>
/// Stands in for the QuickbaseEngine's cached query service: records every
/// query and answers with whatever the spec set up.
/// </summary>
public sealed class FakeQuickbaseQueryService : IQuickbaseQueryService
{
    private Func<QuickbaseQuery, string> _respond = _ => """{ "data": [], "fields": [], "metadata": { "totalRecords": 0 } }""";

    public ConcurrentQueue<QuickbaseQuery> Queries { get; } = new();

    public DateTimeOffset FetchedAt { get; } = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    public void RespondWith(string responseJson) => _respond = _ => responseJson;

    public void RespondWith(Func<QuickbaseQuery, string> respond) => _respond = respond;

    public void FailWith(int statusCode) => _respond = _ => throw new QuickbaseApiException("Quickbase is down.", statusCode);

    public Task<QuickbaseQueryResult> QueryAsync(QuickbaseQuery query, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        Queries.Enqueue(query);
        return Task.FromResult(new QuickbaseQueryResult(_respond(query), QuickbaseResultSource.Quickbase, FetchedAt));
    }
}
