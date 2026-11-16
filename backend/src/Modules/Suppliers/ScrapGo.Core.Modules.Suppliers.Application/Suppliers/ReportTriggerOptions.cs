using System.ComponentModel.DataAnnotations;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// Where the portal triggers its retired Qhuckbase-style reports. Bound from the
/// <c>Reports</c> section (<c>Reports__MissingInvoicesWebhookUrl</c>,
/// <c>Reports__RemittanceWebhookUrl</c>, <c>Reports__SharedWebhookToken</c>).
/// </summary>
/// <remarks>
/// These are secrets: the shared token authenticates the webhook calls. They live
/// in user-secrets locally and Secret Manager in production, never in appsettings
/// or in the frontend bundle. The frontend never sees them — it calls the
/// <c>POST /api/suppliers/reports/…</c> endpoints and the API forwards them.
/// </remarks>
public sealed class ReportTriggerOptions
{
    public const string SectionName = "Reports";

    /// <summary>Full n8n webhook URL for the Missing Invoice report.</summary>
    [Required]
    [Url]
    public string MissingInvoicesWebhookUrl { get; set; } = string.Empty;

    /// <summary>Full n8n webhook URL for the Remittance report.</summary>
    [Required]
    [Url]
    public string RemittanceWebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Token put into both webhook URLs where they say <c>{secretToken}</c>, or else
    /// appended as <c>?token=…</c>. Set it in user-secrets / Secret Manager only.
    /// </summary>
    [Required]
    public string SharedWebhookToken { get; set; } = string.Empty;

    /// <summary>Timeout for the outbound webhook call.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}