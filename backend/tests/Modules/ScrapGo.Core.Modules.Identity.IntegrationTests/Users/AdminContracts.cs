// ADMIN-API-STATUS Task 2: admin contract shapes and the request validation rules handlers will apply.
using System.Net.Http.Json;
using System.Text.Json;
using ScrapGo.Core.Modules.Identity.Application.Organizations;
using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class AdminContracts
{
    // Present on the wire now, populated in Task 5.
    public class Given_a_caller_calling_users_me(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_body_carries_roles_and_scoped_permissions_arrays()
        {
            var response = await fixture.SendUsersMeAsync(fixture.CreateToken(Guid.NewGuid().ToString(), email: "me@example.com"));
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Array, body.GetProperty("roles").ValueKind);
            Assert.Equal(JsonValueKind.Array, body.GetProperty("permissions").ValueKind);
        }
    }

    public class Given_an_assign_role_command
    {
        [Theory]
        [InlineData(5, null)]
        [InlineData(5, 12)]
        public void A_positive_role_id_with_no_or_a_positive_organization_is_well_formed(int? roleId, int? organizationId) =>
            Assert.True(new AssignRoleCommand("uid", UserId: 1, roleId, organizationId).IsWellFormed);

        [Theory]
        [InlineData(null, null)]
        [InlineData(0, null)]
        [InlineData(-3, null)]
        [InlineData(5, 0)]
        [InlineData(5, -1)]
        public void A_missing_or_non_positive_id_is_not(int? roleId, int? organizationId) =>
            Assert.False(new AssignRoleCommand("uid", UserId: 1, roleId, organizationId).IsWellFormed);
    }

    public class Given_an_update_organization_command
    {
        [Fact]
        public void A_name_with_a_letter_or_digit_is_well_formed() =>
            Assert.True(new UpdateOrganizationCommand("uid", OrganizationId: 1, "Acme Metals").IsWellFormed);

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("--- !!")]
        public void A_blank_or_symbol_only_name_is_not(string? name) =>
            Assert.False(new UpdateOrganizationCommand("uid", OrganizationId: 1, name).IsWellFormed);
    }

    public class Given_a_page_request
    {
        [Fact]
        public void Omitted_values_take_the_defaults()
        {
            var request = new PageRequest();

            Assert.True(request.IsValid);
            Assert.Equal(1, request.ResolvedPage);
            Assert.Equal(PageRequest.DefaultPageSize, request.ResolvedPageSize);
            Assert.Equal(0, request.Skip);
        }

        [Fact]
        public void Skip_is_the_offset_of_the_requested_page() =>
            Assert.Equal(40, new PageRequest(Page: 3, PageSize: 20).Skip);

        [Theory]
        [InlineData(0, null)]
        [InlineData(-1, null)]
        [InlineData(PageRequest.MaxPage + 1, null)]
        [InlineData(null, 0)]
        [InlineData(null, PageRequest.MaxPageSize + 1)]
        public void Out_of_range_values_are_invalid(int? page, int? pageSize) =>
            Assert.False(new PageRequest(page, pageSize).IsValid);

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(25, 1)]
        [InlineData(26, 2)]
        public void Total_pages_rounds_up(int totalCount, int expectedPages) =>
            Assert.Equal(expectedPages, PagedResult<int>.Create([], new PageRequest(PageSize: 25), totalCount).TotalPages);
    }
}
