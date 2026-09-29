using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ScrapGo.Core.Shared.Infrastructure.Web;

/// <summary>
/// Security headers on every response (ported from identity-platform). This
/// is a JSON API, so the Content-Security-Policy is deny-all.
/// </summary>
/// <remarks>
/// HSTS is set here rather than with the built-in <c>UseHsts()</c>, which
/// silently skips localhost. Development skips it too, so local HTTP never
/// gets pinned to HTTPS.
/// </remarks>
public sealed class SecureHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";

            // Swagger UI needs scripts and styles, so it keeps its own defaults.
            if (!context.Request.Path.StartsWithSegments("/swagger"))
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            if (!environment.IsDevelopment())
            {
                headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
