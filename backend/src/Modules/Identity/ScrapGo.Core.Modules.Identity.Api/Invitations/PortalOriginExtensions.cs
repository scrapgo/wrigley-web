namespace ScrapGo.Core.Modules.Identity.Api.Invitations;

public static class PortalOriginExtensions
{
    /// <summary>
    /// The portal the request comes from: the browser's <c>Origin</c>, or the
    /// host Firebase Hosting forwarded the call from. Only an origin on
    /// <c>Invitations:FrontendOrigins</c> is ever used for links.
    /// </summary>
    public static string? PortalOrigin(this HttpRequest request)
    {
        if (request.Headers.Origin.ToString() is { Length: > 0 } origin && origin != "null")
        {
            return origin;
        }

        return request.Headers["X-Forwarded-Host"].ToString() is { Length: > 0 } forwardedHost
            ? $"https://{forwardedHost.Split(',')[0].Trim()}"
            : null;
    }
}
