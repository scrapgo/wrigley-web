using System.Collections.Concurrent;
using System.Text;

namespace ScrapGo.Core.Modules.QuickbaseEngine.IntegrationTests.Fixtures;

/// <summary>
/// Stands in for <c>api.quickbase.com</c>. It records every request, and
/// answers each with a response that is distinct per call (the call number is
/// embedded), so a spec can tell a cached answer from a fresh one. Set
/// <see cref="FailWith"/> to simulate an outage or rate limit.
/// </summary>
public sealed class FakeQuickbaseApi
{
    private int _callCount;

    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    public int CallCount => Volatile.Read(ref _callCount);

    /// <summary>When set, calls fail with this status (e.g. 429, 503). See <see cref="FailFirst"/>.</summary>
    public HttpStatusCode? FailWith { get; set; }

    /// <summary>
    /// When set, only this many calls fail with <see cref="FailWith"/> before
    /// the fake recovers. This simulates a transient blip. Null means every
    /// call fails.
    /// </summary>
    public int? FailFirst { get; set; }

    private bool ShouldFail(int callNumber) => FailWith is not null && (FailFirst is not { } limit || callNumber <= limit);

    /// <summary>The body the fake returns for its <paramref name="callNumber"/>-th call (1-based).</summary>
    public static string ResponseFor(int callNumber) =>
        $$$"""{"data":[{"3":{"value":{{{callNumber}}}}}],"fields":[{"id":3,"label":"Record ID#","type":"recordid"}],"metadata":{"totalRecords":1,"numRecords":1,"numFields":1,"skip":0}}""";

    public HttpMessageHandler CreateHandler() => new Handler(this);

    private sealed class Handler(FakeQuickbaseApi api) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var callNumber = Interlocked.Increment(ref api._callCount);
            api.Requests.Enqueue(new RecordedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            return api.ShouldFail(callNumber)
                ? new HttpResponseMessage(api.FailWith!.Value)
                {
                    Content = new StringContent("""{"message":"Service unavailable","description":"Simulated failure"}""", Encoding.UTF8, "application/json"),
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ResponseFor(callNumber), Encoding.UTF8, "application/json"),
                };
        }
    }
}

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string? Body);
