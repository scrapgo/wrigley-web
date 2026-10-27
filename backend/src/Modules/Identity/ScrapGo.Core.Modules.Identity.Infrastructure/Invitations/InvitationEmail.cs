using System.Globalization;
using System.Net;
using System.Text;
using ScrapGo.Core.Modules.Identity.Application.Invitations;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Invitations;

/// <summary>The invitation email as an RFC 2822 message, with plain-text and HTML parts.</summary>
public static class InvitationEmail
{
    public const string Subject = "You're invited to ScrapGo";

    public static string Build(string sender, InvitationMessage message)
    {
        var expires = message.ExpiresAt.UtcDateTime.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
        var link = message.AcceptLink;
        var boundary = $"scrapgo-{Guid.NewGuid():N}";

        var text = $"""
            You've been invited to the ScrapGo portal.

            Accept the invitation and create your account:
            {link}

            This link works once and expires on {expires}. If you weren't expecting this, you can ignore this email.
            """;

        var html = $"""
            <p>You've been invited to the ScrapGo portal.</p>
            <p><a href="{WebUtility.HtmlEncode(link)}">Accept the invitation and create your account</a></p>
            <p>This link works once and expires on {WebUtility.HtmlEncode(expires)}. If you weren't expecting this, you can ignore this email.</p>
            """;

        return new StringBuilder()
            .Append("From: ScrapGo <").Append(HeaderValue(sender)).Append(">\r\n")
            .Append("To: ").Append(HeaderValue(message.Email)).Append("\r\n")
            .Append("Subject: ").Append(Subject).Append("\r\n")
            .Append("MIME-Version: 1.0\r\n")
            .Append("Content-Type: multipart/alternative; boundary=\"").Append(boundary).Append("\"\r\n")
            .Append("\r\n")
            .Append("--").Append(boundary).Append("\r\n")
            .Append("Content-Type: text/plain; charset=UTF-8\r\n")
            .Append("Content-Transfer-Encoding: 8bit\r\n\r\n")
            .Append(text.ReplaceLineEndings("\r\n")).Append("\r\n")
            .Append("--").Append(boundary).Append("\r\n")
            .Append("Content-Type: text/html; charset=UTF-8\r\n")
            .Append("Content-Transfer-Encoding: 8bit\r\n\r\n")
            .Append(html.ReplaceLineEndings("\r\n")).Append("\r\n")
            .Append("--").Append(boundary).Append("--\r\n")
            .ToString();
    }

    /// <summary>Header values can't carry line breaks (header injection).</summary>
    private static string HeaderValue(string value) => value.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
}
