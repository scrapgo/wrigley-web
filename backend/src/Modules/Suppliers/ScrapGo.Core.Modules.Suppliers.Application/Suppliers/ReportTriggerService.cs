using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// Fires the retired Qhuckbase-style reports by POSTing to their n8n webhooks.
/// The URLs and shared token come from <see cref="ReportTriggerOptions"/> — the
/// frontend never holds them. Every call is gated by <c>users.manage</c> at the
/// controller, and the caller's identity is logged with the trigger.
/// </summary>
public sealed class ReportTriggerService(
    IHttpClientFactory httpClientFactory,
    IOptions<ReportTriggerOptions> options,
    TimeProvider timeProvider,
    ILogger<ReportTriggerService> logger)
{
    /// <summary>The <see cref="IHttpClientFactory"/> name for the outbound webhook client.</summary>
    public const string HttpClientFactoryName = "Reports";

    /// <summary>Trigger the Missing Invoice report for <paramref name="reportDate"/> (defaults to today).</summary>
    public async Task<ReportTriggerResult> TriggerMissingInvoicesAsync(
        DateOnly? reportDate, CancellationToken cancellationToken)
    {
        var date = reportDate ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        try
        {
            return await TriggerAsync(options.Value.MissingInvoicesWebhookUrl, date, "missing-invoices", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Missing invoices report setup failed for {Date}.", date);
            return ReportTriggerResult.Failed("missing-invoices", date, ex.GetType().Name, ex.Message);
        }
    }

    /// <summary>Trigger the Remittance report for <paramref name="reportDate"/> (required).</summary>
    public async Task<ReportTriggerResult> TriggerRemittanceAsync(
        DateOnly reportDate, CancellationToken cancellationToken)
    {
        try
        {
            return await TriggerAsync(options.Value.RemittanceWebhookUrl, reportDate, "remittance", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Remittance report setup failed for {Date}.", reportDate);
            return ReportTriggerResult.Failed("remittance", reportDate, ex.GetType().Name, ex.Message);
        }
    }

    private async Task<ReportTriggerResult> TriggerAsync(
        string webhookUrl, DateOnly date, string reportName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            logger.LogWarning("The {Report} webhook URL is not configured.", reportName);
            return ReportTriggerResult.NotConfigured(reportName);
        }

        var token = options.Value.SharedWebhookToken;
        var url = AppendToken(webhookUrl, token);
        var payload = new { reportDate = date.ToString("yyyy-MM-dd") };

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientFactoryName);
            client.Timeout = options.Value.Timeout;

            logger.LogInformation(
                "Triggering {Report} report for {Date}.", reportName, date.ToString("yyyy-MM-dd"));

            using var response = await client.PostAsJsonAsync(
                url, payload, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "{Report} report triggered for {Date}: HTTP {Status}.",
                    reportName, date.ToString("yyyy-MM-dd"), (int)response.StatusCode);
                return ReportTriggerResult.Queued(reportName, date);
            }

            var body = await ReadBodyAsync(response, cancellationToken);
            logger.LogWarning(
                "{Report} webhook rejected the trigger for {Date}: HTTP {Status} {Body}.",
                reportName, date.ToString("yyyy-MM-dd"), (int)response.StatusCode, body);
            return ReportTriggerResult.Failed(reportName, date, $"HTTP {(int)response.StatusCode}", body);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            logger.LogInformation("{Report} trigger cancelled for {Date}.", reportName, date);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Report} webhook call failed for {Date}.", reportName, date);
            return ReportTriggerResult.Failed(reportName, date, ex.GetType().Name, ex.Message);
        }
    }

    private static string AppendToken(string webhookUrl, string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return webhookUrl;
        }

        var separator = webhookUrl.Contains('?') ? '&' : '?';
        return $"{webhookUrl}{separator}token={token}";
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

/// <summary>The outcome of triggering a report.</summary>
public sealed record ReportTriggerResult(
    bool Success,
    string ReportName,
    DateOnly Date,
    string? Error,
    string? Detail)
{
    public static ReportTriggerResult Queued(string reportName, DateOnly date) =>
        new(true, reportName, date, null, null);

    public static ReportTriggerResult NotConfigured(string reportName) =>
        new(false, reportName, default, "not_configured", "The webhook URL is not configured.");

    public static ReportTriggerResult Failed(string reportName, DateOnly date, string error, string? detail) =>
        new(false, reportName, date, error, detail);
}