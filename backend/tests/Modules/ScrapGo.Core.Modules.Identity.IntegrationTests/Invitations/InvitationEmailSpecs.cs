// Invitation emails: the Gmail sender's request, and the invitation API's
// behaviour when sending works and when it fails.
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ScrapGo.Core.Modules.Identity.Application.Invitations;
using ScrapGo.Core.Modules.Identity.Infrastructure.Invitations;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Invitations;

public class InvitationEmailSpecs
{
    private static readonly InvitationMessage Message = new(
        7, "buyer@acme.example", "https://portal.scrapgo.example/invite/accept?token=abc123", new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));

    public class Given_the_gmail_sender
    {
        [Fact]
        public async Task It_posts_the_message_to_the_gmail_api_with_the_access_token()
        {
            var handler = new RecordingHandler(HttpStatusCode.OK);

            await NewSender(handler).SendAsync(Message, CancellationToken.None);

            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(GmailInvitationSender.SendUrl, request.Uri);
            Assert.Equal("Bearer fake-access-token", request.Authorization);
        }

        [Fact]
        public async Task The_message_goes_to_the_invitee_with_the_accept_link_and_expiry()
        {
            var handler = new RecordingHandler(HttpStatusCode.OK);

            await NewSender(handler).SendAsync(Message, CancellationToken.None);

            var raw = JsonDocument.Parse(handler.Requests.Single().Body).RootElement.GetProperty("raw").GetString()!;
            var mime = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(raw));
            Assert.Contains("From: ScrapGo <noreply@scrapgo.example>", mime);
            Assert.Contains("To: buyer@acme.example", mime);
            Assert.Contains($"Subject: {InvitationEmail.Subject}", mime);
            Assert.Contains("https://portal.scrapgo.example/invite/accept?token=abc123", mime);
            Assert.Contains("November 3, 2026", mime);
        }

        // Sad path, kept separate.

        [Fact]
        public async Task A_gmail_error_throws()
        {
            var handler = new RecordingHandler(HttpStatusCode.Forbidden);

            await Assert.ThrowsAsync<HttpRequestException>(() => NewSender(handler).SendAsync(Message, CancellationToken.None));
        }

        private static GmailInvitationSender NewSender(RecordingHandler handler) =>
            new(new SingleClientFactory(handler), new FakeTokenSource(), Options.Create(new GmailOptions
            {
                Sender = "noreply@scrapgo.example",
                ServiceAccountJson = "{}",
            }));
    }

    public class Given_sending_works(CapturingSenderFixture fixture) : IClassFixture<CapturingSenderFixture>
    {
        [Fact]
        public async Task Creating_an_invitation_emails_the_accept_link_to_the_invitee()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = $"buyer-{Guid.NewGuid():N}@acme.example";

            var response = await CreateAsync(fixture, inviterUid, email);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("emailSent").GetBoolean());
            var sent = Assert.Single(fixture.Sent, m => m.Email == email);
            Assert.Equal(body.GetProperty("acceptLink").GetString(), sent.AcceptLink);
            Assert.StartsWith($"{IdentitySpecFixture.FrontendBaseUrl}/invite/accept?token=", sent.AcceptLink);
        }

        [Fact]
        public async Task Resending_emails_the_new_link()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = $"buyer-{Guid.NewGuid():N}@acme.example";
            var created = await (await CreateAsync(fixture, inviterUid, email)).Content.ReadFromJsonAsync<JsonElement>();
            var id = created.GetProperty("invitation").GetProperty("id").GetInt32();

            var response = await fixture.SendAsync(HttpMethod.Post, $"/api/admin/invitations/{id}/resend", fixture.CreateToken(inviterUid));

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("emailSent").GetBoolean());
            Assert.Equal(2, fixture.Sent.Count(m => m.Email == email));
            Assert.Equal(body.GetProperty("acceptLink").GetString(), fixture.Sent.Last(m => m.Email == email).AcceptLink);
        }
    }

    // Links point back at whichever listed portal address the admin used.
    public class Given_a_portal_origin(CapturingSenderFixture fixture) : IClassFixture<CapturingSenderFixture>
    {
        private const string FirebasePortal = "https://wrigley-cloud-prod.web.app";

        [Fact]
        public async Task A_listed_origin_is_used_for_the_accept_link()
        {
            var link = await CreateLinkAsync(fixture, request => request.Headers.Add("Origin", FirebasePortal));

            Assert.StartsWith($"{FirebasePortal}/invite/accept?token=", link);
        }

        // Firebase Hosting forwards /api calls to Cloud Run with the portal host here.
        [Fact]
        public async Task The_forwarded_host_is_used_when_there_is_no_origin()
        {
            var link = await CreateLinkAsync(fixture, request => request.Headers.Add("X-Forwarded-Host", "wrigley-cloud-prod.firebaseapp.com"));

            Assert.StartsWith("https://wrigley-cloud-prod.firebaseapp.com/invite/accept?token=", link);
        }

        [Fact]
        public async Task An_unlisted_origin_falls_back_to_the_configured_base_url()
        {
            var link = await CreateLinkAsync(fixture, request => request.Headers.Add("Origin", "https://evil.example"));

            Assert.StartsWith($"{IdentitySpecFixture.FrontendBaseUrl}/invite/accept?token=", link);
        }

        private static async Task<string> CreateLinkAsync(CapturingSenderFixture fixture, Action<HttpRequestMessage> decorate)
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/invitations")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", fixture.CreateToken(inviterUid)) },
                Content = JsonContent.Create(InvitationBody(fixture, $"buyer-{Guid.NewGuid():N}@acme.example")),
            };
            decorate(request);

            var response = await fixture.Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("acceptLink").GetString()!;
        }
    }

    // Without Gmail settings nothing is emailed, and the admin is told to share the link.
    public class Given_email_is_not_configured(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_invitation_is_created_with_email_sent_false()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);

            var response = await CreateAsync(fixture, inviterUid, $"buyer-{Guid.NewGuid():N}@acme.example");

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.False((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("emailSent").GetBoolean());
        }
    }

    // Sad path, kept separate: a failed email never fails the invitation.

    public class Given_sending_fails(FailingSenderFixture fixture) : IClassFixture<FailingSenderFixture>
    {
        [Fact]
        public async Task The_invitation_is_still_created_with_its_link_and_email_sent_false()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = $"buyer-{Guid.NewGuid():N}@acme.example";

            var response = await CreateAsync(fixture, inviterUid, email);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.GetProperty("emailSent").GetBoolean());
            Assert.False(string.IsNullOrEmpty(body.GetProperty("acceptLink").GetString()));
            Assert.True(await fixture.DbContext.Invitations.AnyAsync(i => i.Email == email));
        }
    }

    /// <summary>The host with a sender that records every message.</summary>
    public sealed class CapturingSenderFixture : IdentitySpecFixture
    {
        public ConcurrentQueue<InvitationMessage> Sent { get; } = new();

        protected override void ConfigureTestServices(IServiceCollection services) =>
            services.Replace(ServiceDescriptor.Singleton<IInvitationSender>(new CapturingSender(Sent)));
    }

    /// <summary>The host with a sender that always fails.</summary>
    public sealed class FailingSenderFixture : IdentitySpecFixture
    {
        protected override void ConfigureTestServices(IServiceCollection services) =>
            services.Replace(ServiceDescriptor.Singleton<IInvitationSender>(new FailingSender()));
    }

    private sealed class CapturingSender(ConcurrentQueue<InvitationMessage> sent) : IInvitationSender
    {
        public Task<bool> SendAsync(InvitationMessage message, CancellationToken cancellationToken)
        {
            sent.Enqueue(message);
            return Task.FromResult(true);
        }
    }

    private sealed class FailingSender : IInvitationSender
    {
        public Task<bool> SendAsync(InvitationMessage message, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Gmail API returned 503.");
    }

    private sealed class FakeTokenSource : IGmailAccessTokenSource
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult("fake-access-token");
    }

    private sealed record RecordedRequest(HttpMethod Method, string Uri, string? Authorization, string Body);

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));

            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static Task<HttpResponseMessage> CreateAsync(IdentitySpecFixture fixture, string inviterUid, string email) =>
        fixture.SendAsync(HttpMethod.Post, "/api/admin/invitations", fixture.CreateToken(inviterUid), InvitationBody(fixture, email));

    private static object InvitationBody(IdentitySpecFixture fixture, string email) => new
    {
        email,
        roleIds = new[] { fixture.DbContext.Roles.AsNoTracking().Where(r => r.Key == DefaultRoleNames.User).Select(r => r.Id).Single() },
        supplierRecordIds = new[] { 17511 },
    };

    private static async Task<string> SeedScrapGoUserAsync(IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
        var roleId = await fixture.DbContext.Roles.AsNoTracking().Where(r => r.Key == DefaultRoleNames.ScrapGoUser).Select(r => r.Id).SingleAsync();
        await fixture.AssignRoleAsync(userId, roleId);
        return uid;
    }
}
