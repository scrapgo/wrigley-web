// STORY-012: Create an organization; the creator becomes its OrganizationAdministrator.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Organizations;

public class CreateOrganization
{
    public class Given_an_authenticated_user_with_no_membership_yet(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private readonly string _uid = Guid.NewGuid().ToString();
        private HttpResponseMessage _response = null!;
        private int _organizationId;

        public async Task InitializeAsync()
        {
            _response = await PostOrganizationAsync(fixture, fixture.CreateToken(_uid), $"Acme Corp {_uid}");
            if (_response.IsSuccessStatusCode)
            {
                _organizationId = (await _response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public void The_response_is_two_hundred_one_with_the_org_id()
        {
            Assert.Equal(HttpStatusCode.Created, _response.StatusCode);
            Assert.NotEqual(0, _organizationId);
            Assert.Equal($"/api/organizations/{_organizationId}", _response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task The_organization_is_created_active()
        {
            var organization = await fixture.DbContext.Organizations.AsNoTracking().SingleAsync(o => o.Id == _organizationId);

            Assert.Equal(OrganizationStatus.Active, organization.Status);
        }

        [Fact]
        public async Task The_creator_has_an_active_membership()
        {
            var creatorId = await CreatorIdAsync();

            Assert.True(await fixture.DbContext.OrganizationMemberships.AnyAsync(
                m => m.OrganizationId == _organizationId && m.UserId == creatorId && m.Status == MembershipStatus.Active));
        }

        [Fact]
        public async Task The_creator_holds_organization_administrator_in_that_organization()
        {
            var creatorId = await CreatorIdAsync();

            Assert.True(await fixture.DbContext.UserRoles.AnyAsync(ur =>
                ur.OrganizationId == _organizationId
                && ur.UserId == creatorId
                && ur.Role.Name == DefaultRoleNames.OrganizationAdministrator
                && ur.Role.OrganizationId == null));
        }

        [Fact]
        public async Task An_organization_created_audit_row_is_written() =>
            Assert.Equal(1, await fixture.DbContext.AuditLogs.CountAsync(
                a => a.EventType == IdentityAuditEventTypes.OrganizationCreated && a.OrganizationId == _organizationId));

        private Task<int> CreatorIdAsync() =>
            fixture.DbContext.Users.Where(u => u.IdentityPlatformUid == _uid).Select(u => u.Id).SingleAsync();
    }

    // Sad paths, kept separate.

    public class Given_a_slug_that_already_exists(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Post_returns_four_hundred_nine_with_reason_duplicate_slug()
        {
            var first = await PostOrganizationAsync(fixture, fixture.CreateToken(Guid.NewGuid().ToString()), "Acme Corp");
            first.EnsureSuccessStatusCode();

            // A different name that normalizes to the same slug ("acme-corp").
            var response = await PostOrganizationAsync(fixture, fixture.CreateToken(Guid.NewGuid().ToString()), "  ACME   corp! ");

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("duplicate_slug", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_an_invalid_name(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("   ")]
        [InlineData("!!!")]
        public async Task Post_returns_four_hundred_with_reason_invalid_name(string name)
        {
            var response = await PostOrganizationAsync(fixture, fixture.CreateToken(Guid.NewGuid().ToString()), name);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_name", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    private static Task<HttpResponseMessage> PostOrganizationAsync(IdentitySpecFixture fixture, string bearerToken, string? name) =>
        fixture.SendAsync(HttpMethod.Post, "/api/organizations", bearerToken, new { name });
}
