using System.Globalization;
using System.Net;
using System.Text;
using ScrapGo.Core.Modules.Identity.Application.Mfa;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Mfa;

/// <summary>The sign-in code email as an RFC 2822 message, with plain-text and HTML parts.</summary>
public static class MfaCodeEmail
{
    public const string Subject = "Your ScrapGo sign-in code";

    public static string Build(string sender, MfaCodeMessage message)
    {
        var expires = message.ExpiresAt.UtcDateTime.ToString("h:mm tt 'UTC'", CultureInfo.InvariantCulture);
        var boundary = $"scrapgo-{Guid.NewGuid():N}";
        var note = $"It expires at {expires}. If you didn't just sign in to ScrapGo, someone may have your password: change it, and don't share this code.";

        var text = $"""
            Your ScrapGo sign-in code is:

            {message.Code}

            {note}
            """;

        var html = $"""
            <p>Your ScrapGo sign-in code is:</p>
            <p style="font-size:28px;font-weight:bold;letter-spacing:6px">{WebUtility.HtmlEncode(message.Code)}</p>
            <p>{WebUtility.HtmlEncode(note)}</p>
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
