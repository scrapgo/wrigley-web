using System.Buffers.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;
using ScrapGo.Core.Modules.Identity.Application.Invitations;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Invitations;

/// <summary>
/// Sends invitation emails through the Gmail API as the configured Workspace
/// mailbox (<see cref="GmailOptions.Sender"/>). The accept link carries the
/// one-time token, so it is never logged.
/// </summary>
public sealed class GmailInvitationSender(
    IHttpClientFactory httpClientFactory, IGmailAccessTokenSource tokens, IOptions<GmailOptions> options) : IInvitationSender
{
    public const string HttpClientName = "gmail";

    public const string SendUrl = "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";

    public async Task SendAsync(InvitationMessage message, CancellationToken cancellationToken)
    {
        var raw = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(InvitationEmail.Build(options.Value.Sender, message)));

        using var request = new HttpRequestMessage(HttpMethod.Post, SendUrl) { Content = JsonContent.Create(new { raw }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAccessTokenAsync(cancellationToken));

        using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gmail API returned {(int)response.StatusCode} sending invitation {message.InvitationId}.", inner: null, response.StatusCode);
        }
    }
}
