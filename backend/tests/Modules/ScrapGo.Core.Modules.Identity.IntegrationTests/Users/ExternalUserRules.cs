// ORG-APP-MODULE-MODEL Task 10: external (customer) users may administer their own organization and
// applications (Decision 4a) but never hold platform roles; MFA for them is reserved behind a flag (4b).
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Application.Authorization;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class ExternalUserRules
{
    public class Given_an_external_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_may_be_made_their_organizations_administrator()
        {
            var (orgAdminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, externalId) = await fixture.SeedUserAsync("customer@acme.example");
            await fixture.SeedMembershipAsync(externalId, organizationId);

            var response = await fixture.SendAsync(
                HttpMethod.Post, $"/api/users/{externalId}/roles", fixture.CreateToken(orgAdminUid), new { roleId = 1, organizationId });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Assigning_them_a_platform_role_returns_four_hundred_with_reason_external_user_not_allowed()
        {
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (_, externalId) = await fixture.SeedUserAsync("customer@acme.example");
            var platformAdminRoleId = await fixture.DbContext.Roles.AsNoTracking()
                .Where(r => r.Name == DefaultRoleNames.PlatformAdministrator && r.OrganizationId == null)
                .Select(r => r.Id)
                .SingleAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Post, $"/api/users/{externalId}/roles", fixture.CreateToken(platformUid), new { roleId = platformAdminRoleId });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("external_user_not_allowed", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task The_bootstrap_command_refuses_them()
        {
            var (uid, _) = await fixture.SeedUserAsync("customer@acme.example");

            await using var scope = fixture.Services.CreateAsyncScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<BootstrapPlatformAdministratorHandler>()
                .HandleAsync(new BootstrapPlatformAdministratorCommand(uid), CancellationToken.None);

            Assert.Equal(BootstrapPlatformAdministratorOutcome.ExternalUserNotAllowed, outcome);
        }

        // Decision 4b: off by default.
        [Fact]
        public async Task Without_the_mfa_flag_a_token_without_a_second_factor_is_accepted()
        {
            var (uid, _) = await fixture.SeedUserAsync("customer@acme.example");

            Assert.Equal(HttpStatusCode.OK, (await fixture.SendUsersMeAsync(fixture.CreateToken(uid))).StatusCode);
        }
    }

    public class Given_the_mfa_requirement_is_turned_on(MfaRequiredIdentitySpecFixture fixture) : IClassFixture<MfaRequiredIdentitySpecFixture>
    {
        [Fact]
        public async Task An_external_user_without_a_second_factor_gets_four_hundred_three_with_reason_mfa_required()
        {
            var (uid, _) = await fixture.SeedUserAsync("customer@acme.example");

            var response = await fixture.SendUsersMeAsync(fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("mfa_required", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task An_external_user_with_a_second_factor_is_allowed()
        {
            var (uid, _) = await fixture.SeedUserAsync("customer@acme.example");

            var response = await fixture.SendUsersMeAsync(fixture.CreateToken(uid, signInProvider: "password", signInSecondFactor: "phone"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task An_internal_user_is_never_affected()
        {
            var (uid, _) = await fixture.SeedUserAsync("staff@scrapgo.example", UserClassification.Internal);

            Assert.Equal(HttpStatusCode.OK, (await fixture.SendUsersMeAsync(fixture.CreateToken(uid))).StatusCode);
        }
    }
}
