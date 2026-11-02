namespace ScrapGo.Core.Modules.Identity.Infrastructure.Invitations;

/// <summary>
/// The <c>Invitations:Gmail</c> configuration section: invitation emails go out
/// through the Gmail API as <see cref="Sender"/>, using a Google Workspace
/// service account with domain-wide delegation for the <c>gmail.send</c> scope.
/// </summary>
public sealed class GmailOptions
{
    public const string SectionName = "Invitations:Gmail";

    /// <summary>The Workspace mailbox the email comes from, e.g. <c>noreply@scrapgo.com</c>.</summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>The service account's JSON key (a secret: user-secrets locally, Secret Manager in GCP).</summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>Both values set; otherwise invitations are only logged.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Sender) && !string.IsNullOrWhiteSpace(ServiceAccountJson);
}
