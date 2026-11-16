using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ScrapGo.Core.Modules.Suppliers.Api.Suppliers;

/// <summary>
/// Triggers the retired Qhuckbase-style reports through their n8n webhooks.
/// The URLs and shared token live in <c>Reports</c> configuration (user-secrets /
/// Secret Manager); the frontend never holds them. Needs <c>users.manage</c>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/suppliers/reports")]
[Produces("application/json")]
public sealed class ReportsController(ReportTriggerService reports) : ControllerBase
{
    /// <summary>Trigger the Missing Invoice report for a date (defaults to today).</summary>
    [HttpPost("missing-invoices")]
    [RequirePermission(ReportPermissions.Trigger)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> MissingInvoices(
        [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var result = await reports.TriggerMissingInvoicesAsync(date, cancellationToken);
        return result.Success
            ? Ok(new ReportTriggerResponse(result.ReportName, result.Date.ToString("yyyy-MM-dd"), true, null, null))
            : Problem(
                title: $"The {result.ReportName} report could not be triggered.",
                detail: result.Detail ?? "The webhook URL is not configured.",
                statusCode: StatusStatusFor(result.Error));
    }

    /// <summary>Trigger the Remittance report for a date (required).</summary>
    [HttpPost("remittance")]
    [RequirePermission(ReportPermissions.Trigger)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Remittance(
        [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var result = await reports.TriggerRemittanceAsync(date, cancellationToken);
        return result.Success
            ? Ok(new ReportTriggerResponse(result.ReportName, result.Date.ToString("yyyy-MM-dd"), true, null, null))
            : Problem(
                title: $"The {result.ReportName} report could not be triggered.",
                detail: result.Detail ?? "The webhook URL is not configured.",
                statusCode: StatusStatusFor(result.Error));
    }

    private static int StatusStatusFor(string? error) => error switch
    {
        "not_configured" => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status502BadGateway,
    };
}

/// <summary>
/// The permission reports endpoints check. Defined in the Identity module's
/// catalog and named here by string, because modules never reference each
/// other; a spec pins them to the catalog.
/// </summary>
public static class ReportPermissions
{
    /// <summary>Trigger the retired Qhuckbase-style reports.</summary>
    public const string Trigger = "users.manage";
}

/// <param name="ReportName">The report that was triggered.</param>
/// <param name="ReportDate">The date the report runs for.</param>
/// <param name="Queued">True when the webhook accepted the trigger.</param>
/// <param name="Error">Machine-readable reason, when <paramref name="Queued"/> is false.</param>
/// <param name="Detail">Human-readable explanation, when <paramref name="Queued"/> is false.</param>
public sealed record ReportTriggerResponse(
    string ReportName,
    string ReportDate,
    bool Queued,
    string? Error,
    string? Detail);