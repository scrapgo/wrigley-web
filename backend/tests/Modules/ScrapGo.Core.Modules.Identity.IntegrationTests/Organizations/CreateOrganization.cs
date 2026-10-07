// STORY-012, revised by ORG-APP-MODULE-MODEL Decision 11: only platform administrators create
// organizations, and each is created with a named first OrganizationAdministrator.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Organizations;

public class CreateOrganization
{
    private const string PlatformOrganizationsPath = "/api/admin/organizations";

    public class Given_a_platform_administrator_naming_a_first_admin(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private HttpResponseMessage _response = null!;
        private int _organizationId;
        private int _platformAdminId;
        private int _firstAdminId;

        public async Task InitializeAsync()
        {
            var (platformAdminUid, platformAdminId) = await fixture.SeedPlatformAdministratorAsync();
            var (_, firstAdminId) = await fixture.SeedUserAsync("first-admin@acme.example");
            _platformAdminId = platformAdminId;
            _firstAdminId = firstAdminId;

            _response = await PostOrganizationAsync(fixture, fixture.CreateToken(platformAdminUid), $"Acme Corp {Guid.NewGuid():N}", firstAdminId);
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
        public async Task The_named_first_admin_is_an_active_member_holding_organization_administrator()
        {
            Assert.True(await fixture.DbContext.OrganizationMemberships.AsNoTracking().AnyAsync(
                m => m.OrganizationId == _organizationId && m.UserId == _firstAdminId && m.Status == MembershipStatus.Active));
            Assert.True(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur =>
                ur.OrganizationId == _organizationId
                && ur.UserId == _firstAdminId
                && ur.Role.Name == DefaultRoleNames.OrganizationAdministrator
                && ur.Role.OrganizationId == null));
        }

        // The platform administrator administers from outside: no membership, no role.
        [Fact]
        public async Task The_creating_platform_admin_gets_nothing_in_the_organization()
        {
            Assert.False(await fixture.DbContext.OrganizationMemberships.AsNoTracking()
                .AnyAsync(m => m.OrganizationId == _organizationId && m.UserId == _platformAdminId));
            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking()
                .AnyAsync(ur => ur.OrganizationId == _organizationId && ur.UserId == _platformAdminId));
        }

        [Fact]
        public async Task Organization_created_membership_added_and_role_assigned_are_audited_with_the_platform_admin_as_actor()
        {
            var events = await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.OrganizationId == _organizationId)
                .Select(a => new { a.EventType, a.UserId })
                .ToListAsync();

            Assert.Equal(
                [IdentityAuditEventTypes.MembershipAdded, IdentityAuditEventTypes.OrganizationCreated, IdentityAuditEventTypes.RoleAssigned],
                events.Select(e => e.EventType).Order(StringComparer.Ordinal));
            Assert.All(events, e => Assert.Equal(_platformAdminId, e.UserId));
        }
    }

    // Sad paths, kept separate.

    // Self-service creation is gone (Decision 11).
    public class Given_any_signed_in_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_old_self_service_route_no_longer_creates_anything()
        {
            var response = await fixture.SendAsync(
                HttpMethod.Post, "/api/organizations", fixture.CreateToken(Guid.NewGuid().ToString()), new { name = "Self Service Co" });

            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            Assert.False(await fixture.DbContext.Organizations.AsNoTracking().AnyAsync(o => o.Name == "Self Service Co"));
        }

        [Fact]
        public async Task The_platform_route_returns_four_hundred_three_without_organization_create()
        {
            var (uid, _, _) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, firstAdminId) = await fixture.SeedUserAsync();

            var response = await PostOrganizationAsync(fixture, fixture.CreateToken(uid), "Not Allowed Co", firstAdminId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_a_slug_that_already_exists(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Post_returns_four_hundred_nine_with_reason_duplicate_slug()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (_, firstAdminId) = await fixture.SeedUserAsync();
            (await PostOrganizationAsync(fixture, fixture.CreateToken(uid), "Acme Corp", firstAdminId)).EnsureSuccessStatusCode();

            // A different name that normalizes to the same slug ("acme-corp").
            var response = await PostOrganizationAsync(fixture, fixture.CreateToken(uid), "  ACME   corp! ", firstAdminId);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("duplicate_slug", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_invalid_input(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("   ")]
        [InlineData("!!!")]
        public async Task An_invalid_name_returns_four_hundred_with_reason_invalid_name(string name)
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (_, firstAdminId) = await fixture.SeedUserAsync();

            var response = await PostOrganizationAsync(fixture, fixture.CreateToken(uid), name, firstAdminId);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_name", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task No_first_admin_returns_four_hundred_with_reason_invalid_request()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();

            var response = await PostOrganizationAsync(fixture, fixture.CreateToken(uid), "Headless Co", firstAdminUserId: null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task An_unknown_first_admin_returns_four_hundred_four_and_creates_nothing()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();

            var response = await PostOrganizationAsync(fixture, fixture.CreateToken(uid), "Ghost Admin Co", firstAdminUserId: 999_999);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.False(await fixture.DbContext.Organizations.AsNoTracking().AnyAsync(o => o.Name == "Ghost Admin Co"));
        }
    }

    internal static Task<HttpResponseMessage> PostOrganizationAsync(
        IdentitySpecFixture fixture, string bearerToken, string? name, int? firstAdminUserId) =>
        fixture.SendAsync(HttpMethod.Post, PlatformOrganizationsPath, bearerToken, new { name, firstAdminUserId });
}
