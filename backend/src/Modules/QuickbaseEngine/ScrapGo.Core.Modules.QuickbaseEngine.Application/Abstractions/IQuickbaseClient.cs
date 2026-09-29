using ScrapGo.Core.Modules.QuickbaseEngine.Application.Queries;

namespace ScrapGo.Core.Modules.QuickbaseEngine.Application.Abstractions;

/// <summary>
/// The only path to the Quickbase REST API. Everything else, controllers
/// included, goes through Application services built on it.
/// </summary>
public interface IQuickbaseClient
{
    /// <summary>
    /// The realm this client talks to (e.g. <c>scrapgo.quickbase.com</c>).
    /// Part of every cache key, so pointing the app at another realm never
    /// serves the old realm's data.
    /// </summary>
    string Realm { get; }

    /// <summary>Runs <c>POST /v1/records/query</c> and returns the response body verbatim.</summary>
    /// <exception cref="QuickbaseApiException">Non-success status, timeout, or transport failure.</exception>
    Task<string> QueryRecordsAsync(QuickbaseQuery query, CancellationToken cancellationToken);
}

/// <summary>A failed Quickbase call. <see cref="StatusCode"/> is null when no response arrived (timeout, network failure).</summary>
public sealed class QuickbaseApiException(string message, int? statusCode = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int? StatusCode { get; } = statusCode;
}
