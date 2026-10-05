// ADMIN-API-STATUS Task 3: user data access for the admin endpoints.
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Application.Abstractions;
using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class UserDataAccess
{
    public class Given_more_users_than_one_page(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Pages_are_ordered_by_id_and_carry_the_total()
        {
            var seeded = new List<int>();
            for (var i = 0; i < 5; i++)
            {
                seeded.Add((await fixture.SeedUserAsync($"paging{i}@paging.example")).UserId);
            }

            var filter = new UserListFilter(Search: "@paging.example");
            var first = await ListUsersAsync(fixture, filter, new PageRequest(Page: 1, PageSize: 2));
            var third = await ListUsersAsync(fixture, filter, new PageRequest(Page: 3, PageSize: 2));

            Assert.Equal(5, first.TotalCount);
            Assert.Equal(3, first.TotalPages);
            Assert.Equal(seeded[..2], first.Items.Select(u => u.Id));
            Assert.Equal([seeded[4]], third.Items.Select(u => u.Id));
        }
    }

    public class Given_users_with_different_emails_and_statuses(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Search_matches_an_email_substring_case_insensitively()
        {
            var (_, aliceId) = await fixture.SeedUserAsync("Alice.Search@filter.example");
            await fixture.SeedUserAsync("bob@filter.example");

            var result = await ListUsersAsync(fixture, new UserListFilter(Search: "alice.SEARCH"), new PageRequest());

            Assert.Equal([aliceId], result.Items.Select(u => u.Id));
        }

        [Fact]
        public async Task Search_treats_like_wildcards_literally()
        {
            await fixture.SeedUserAsync("wildcard-x@literal.example");
            var (_, underscoreId) = await fixture.SeedUserAsync("wildcard_y@literal.example");

            var result = await ListUsersAsync(fixture, new UserListFilter(Search: "wildcard_"), new PageRequest());

            Assert.Equal([underscoreId], result.Items.Select(u => u.Id));
        }

        [Fact]
        public async Task Status_filter_returns_only_that_status()
        {
            var (_, activeId) = await fixture.SeedUserAsync("active@status.example");
            var (_, disabledId) = await fixture.SeedUserAsync("disabled@status.example");
            var disabled = await fixture.DbContext.Users.SingleAsync(u => u.Id == disabledId);
            disabled.Disable(DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();

            var result = await ListUsersAsync(
                fixture, new UserListFilter(Search: "@status.example", Status: UserStatus.Disabled), new PageRequest());

            var row = Assert.Single(result.Items);
            Assert.Equal(disabledId, row.Id);
            Assert.Equal("Disabled", row.Status);
            Assert.DoesNotContain(result.Items, u => u.Id == activeId);
        }
    }

    public class Given_a_user_to_disable(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Update_status_inside_a_transaction_persists_and_returns_the_previous_status()
        {
            var (_, userId) = await fixture.SeedUserAsync("to-disable@example.com");

            await using var scope = fixture.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var previous = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var before = await users.UpdateStatusAsync(userId, UserStatus.Disabled, DateTimeOffset.UtcNow, ct);
                await unitOfWork.SaveChangesAsync(ct);
                return before;
            }, CancellationToken.None);

            Assert.Equal(UserStatus.Active, previous);
            Assert.Equal(UserStatus.Disabled, await fixture.DbContext.Users.AsNoTracking()
                .Where(u => u.Id == userId).Select(u => u.Status).SingleAsync());
        }

        [Fact]
        public async Task An_unknown_user_returns_null()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            Assert.Null(await users.UpdateStatusAsync(999_999, UserStatus.Disabled, DateTimeOffset.UtcNow, CancellationToken.None));
            Assert.Null(await users.GetByIdAsync(999_999, CancellationToken.None));
        }
    }

    private static async Task<PagedResult<UserSummaryDto>> ListUsersAsync(
        IdentitySpecFixture fixture, UserListFilter filter, PageRequest page)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IAuthorizationQueries>()
            .ListUsersAsync(filter, page, CancellationToken.None);
    }
}
