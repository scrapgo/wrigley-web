namespace ScrapGo.Core.Modules.Identity.Application.Invitations;

/// <summary>The <c>Invitations</c> configuration section.</summary>
public sealed class InvitationOptions
{
    public const string SectionName = "Invitations";

    /// <summary>
    /// The portal's origin, e.g. <c>https://portal.scrapgo.com</c>. Blank
    /// leaves accept links relative (<c>/invite/accept?token=…</c>).
    /// </summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    public string BuildAcceptLink(string token) =>
        $"{FrontendBaseUrl.Trim().TrimEnd('/')}/invite/accept?token={Uri.EscapeDataString(token)}";
}
