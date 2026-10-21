// Admin contract shapes and the request validation rules handlers will apply.
using System.Net.Http.Json;
using System.Text.Json;
using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class AdminContracts
{
    public class Given_a_caller_calling_me(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_body_carries_user_roles_permissions_and_features()
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var response = await fixture.SendMeAsync(fixture.CreateToken(uid, email: "me@example.com"));
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Object, body.GetProperty("user").ValueKind);
            Assert.Equal(JsonValueKind.Array, body.GetProperty("roles").ValueKind);
            Assert.Equal(JsonValueKind.Array, body.GetProperty("permissions").ValueKind);
            Assert.Equal(JsonValueKind.Array, body.GetProperty("features").ValueKind);
        }
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
