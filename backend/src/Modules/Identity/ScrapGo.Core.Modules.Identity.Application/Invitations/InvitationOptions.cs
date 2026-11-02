namespace ScrapGo.Core.Modules.Identity.Application.Invitations;

/// <summary>The <c>Invitations</c> configuration section.</summary>
public sealed class InvitationOptions
{
    public const string SectionName = "Invitations";

    /// <summary>
    /// The portal's origin when the request doesn't come from one of
    /// <see cref="FrontendOrigins"/>, e.g. <c>https://portal.scrapgo.com</c>.
    /// Blank leaves such links relative (<c>/invite/accept?token=…</c>).
    /// </summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Portal origins links may point at, e.g. <c>https://wrigley-cloud-prod.web.app</c>.
    /// An invitation created from one of them links back to it, so every portal
    /// address works. Only listed origins are used, so a forged <c>Origin</c>
    /// header can't send invitees elsewhere.
    /// </summary>
    public string[] FrontendOrigins { get; set; } = [];

    /// <param name="requestOrigin">The calling portal's origin, if known.</param>
    public string BuildAcceptLink(string token, string? requestOrigin = null) =>
        $"{BaseUrlFor(requestOrigin)}/invite/accept?token={Uri.EscapeDataString(token)}";

    /// <summary>Where an internal invitee goes: their first Google sign-in applies the invitation, no token needed.</summary>
    public string BuildSignInLink(string? requestOrigin = null) => $"{BaseUrlFor(requestOrigin)}/login";

    /// <summary>The listed origin matching <paramref name="requestOrigin"/>, else <see cref="FrontendBaseUrl"/>.</summary>
    public string BaseUrlFor(string? requestOrigin)
    {
        var origin = Normalize(requestOrigin);
        var listed = origin.Length == 0
            ? null
            : FrontendOrigins.FirstOrDefault(candidate => string.Equals(Normalize(candidate), origin, StringComparison.OrdinalIgnoreCase));

        return listed is not null ? Normalize(listed) : Normalize(FrontendBaseUrl);
    }

    private static string Normalize(string? url) => (url ?? string.Empty).Trim().TrimEnd('/');
}
