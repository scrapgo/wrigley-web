using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure.Quickbase;

/// <summary>
/// <see cref="IQuickbaseClient"/> over the Quickbase REST API. It is a typed
/// client created by <c>IHttpClientFactory</c>, which configures the base
/// address, timeout, and the realm, authorization and user-agent headers (see
/// the Infrastructure DI extension).
/// </summary>
public sealed class QuickbaseHttpClient(HttpClient httpClient, IOptions<QuickbaseOptions> options) : IQuickbaseClient
{
    /// <summary>Name of the factory client, so tests can replace its primary handler.</summary>
    public const string HttpClientName = "Quickbase";

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Realm => options.Value.RealmHostname;

    public async Task<string> QueryRecordsAsync(QuickbaseQuery query, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync("records/query", RecordsQueryBody.Create(query), RequestJsonOptions, cancellationToken);
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            throw new QuickbaseApiException($"Quickbase query on table '{query.TableId}' failed before a response arrived.", innerException: ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Quickbase error bodies are {"message": "...", "description": "..."}.
                // They carry no credentials, so they are safe in the exception message.
                throw new QuickbaseApiException(
                    $"Quickbase query on table '{query.TableId}' returned {(int)response.StatusCode}: {Truncate(body, 500)}",
                    (int)response.StatusCode);
            }

            return body;
        }
    }

    /// <summary>
    /// Every way the resilience pipeline can give up without a response: the
    /// network failed, the attempt or total timeout fired, or the circuit is
    /// open. All of these surface as <see cref="QuickbaseApiException"/>, so
    /// callers (and stale-if-error) handle a single failure type. A
    /// cancellation the caller requested is not a failure, and propagates as is.
    /// </summary>
    private static bool IsTransportFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or TimeoutRejectedException or BrokenCircuitException
        || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "…");

    /// <summary>The wire shape of <c>POST /v1/records/query</c>.</summary>
    private sealed record RecordsQueryBody(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("select")] IReadOnlyList<int>? Select,
        [property: JsonPropertyName("where")] string? Where,
        [property: JsonPropertyName("sortBy")] IReadOnlyList<SortBody>? SortBy,
        [property: JsonPropertyName("options")] OptionsBody? Options)
    {
        public static RecordsQueryBody Create(QuickbaseQuery query) =>
            new(
                query.TableId,
                query.Select,
                query.Where,
                query.SortBy?.Select(s => new SortBody(s.FieldId, s.Order == QuickbaseSortOrder.Descending ? "DESC" : "ASC")).ToList(),
                query.Skip is null && query.Top is null ? null : new OptionsBody(query.Skip, query.Top));
    }

    private sealed record SortBody(
        [property: JsonPropertyName("fieldId")] int FieldId,
        [property: JsonPropertyName("order")] string Order);

    private sealed record OptionsBody(
        [property: JsonPropertyName("skip")] int? Skip,
        [property: JsonPropertyName("top")] int? Top);
}
