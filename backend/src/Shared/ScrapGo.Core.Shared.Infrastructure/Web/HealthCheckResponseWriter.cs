using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ScrapGo.Core.Shared.Infrastructure.Web;

/// <summary>
/// A JSON health report (overall status plus one entry per check). It replaces
/// identity-platform's HealthChecks.UI writer without pulling in the UI
/// package. Exception details are never written, only the check name, status
/// and duration.
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        return context.Response.WriteAsJsonAsync(
            new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration,
                entries = report.Entries.ToDictionary(
                    e => e.Key,
                    e => new { status = e.Value.Status.ToString(), duration = e.Value.Duration, tags = e.Value.Tags }),
            },
            JsonOptions);
    }
}
