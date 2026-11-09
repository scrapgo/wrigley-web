using System.Buffers.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;
using ScrapGo.Core.Modules.Identity.Application.Mfa;
using ScrapGo.Core.Modules.Identity.Infrastructure.Invitations;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Mfa;

/// <summary>Sends sign-in codes through the Gmail API, as the same mailbox as invitations. The code is never logged.</summary>
public sealed class GmailMfaCodeSender(
    IHttpClientFactory httpClientFactory, IGmailAccessTokenSource tokens, IOptions<GmailOptions> options) : IMfaCodeSender
{
    public async Task<bool> SendAsync(MfaCodeMessage message, CancellationToken cancellationToken)
    {
        var raw = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(MfaCodeEmail.Build(options.Value.Sender, message)));

        using var request = new HttpRequestMessage(HttpMethod.Post, GmailInvitationSender.SendUrl) { Content = JsonContent.Create(new { raw }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAccessTokenAsync(cancellationToken));

        using var response = await httpClientFactory.CreateClient(GmailInvitationSender.HttpClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gmail API returned {(int)response.StatusCode} sending a sign-in code.", inner: null, response.StatusCode);
        }

        return true;
    }
}
