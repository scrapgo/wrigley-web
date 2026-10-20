// A supplier's Other Prospect Information (Documents & Other): Suppliers table
// fields plus the most recent activity from the Call Notes table, two queries
// merged into one response.
namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class OtherProspectInformationSpecs
{
    private const string Path = "/17511/other-prospect-information";

    private static Func<QuickbaseQuery, string> RespondPerTable(string supplier, string activities) =>
        query => query.TableId == "bqrcgnatz" ? supplier : activities;

    public class Given_a_supplier_reader(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Sends_the_supplier_and_latest_activity_queries_and_merges_them()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(RespondPerTable(SupplierResponses.OtherProspectInformation, SupplierResponses.LatestActivity));
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var queries = fixture.Quickbase.Queries.Skip(before).ToList();
            Assert.Equal(2, queries.Count);

            var supplierQuery = queries[0];
            Assert.Equal("bqrcgnatz", supplierQuery.TableId);
            Assert.Equal([3, 77, 152, 175, 157, 215], supplierQuery.Select);
            Assert.Equal("{3.EX.'17511'}", supplierQuery.Where);

            var activityQuery = queries[1];
            Assert.Equal("br8tgw3tp", activityQuery.TableId);
            Assert.Equal([3, 19, 57, 1], activityQuery.Select);
            Assert.Equal("{19.EX.'17511'}", activityQuery.Where);
            var sort = Assert.Single(activityQuery.SortBy!);
            Assert.Equal(1, sort.FieldId);
            Assert.Equal(QuickbaseSortOrder.Descending, sort.Order);
            Assert.Equal(0, activityQuery.Skip);
            Assert.Equal(1, activityQuery.Top);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var information = body.GetProperty("otherProspectInformation");
            Assert.Equal(17511, information.GetProperty("recordId").GetInt32());
            Assert.Equal("Cold Call", information.GetProperty("leadSource").GetString());
            Assert.Equal("1st Class Auto Salvage - Orlando", information.GetProperty("lastReassignedSupplier").GetString());
            Assert.Equal("John Smith", information.GetProperty("lastModifiedBy").GetString());
            Assert.False(information.GetProperty("outOfBusiness").GetBoolean());

            // Empty text comes back as null.
            Assert.Equal(JsonValueKind.Null, information.GetProperty("relatedSupplierOffice").ValueKind);

            var activity = information.GetProperty("latestActivity");
            Assert.Equal(845, activity.GetProperty("recordId").GetInt32());
            Assert.Equal(17511, activity.GetProperty("supplierId").GetInt32());
            Assert.Equal("john@scrapgo.com", activity.GetProperty("supplierAssignedTo").GetString());
            Assert.Equal(new DateTimeOffset(2026, 10, 6, 18, 30, 0, TimeSpan.Zero), activity.GetProperty("lastReassigned").GetDateTimeOffset());

            Assert.Equal("Quickbase", body.GetProperty("freshness").GetProperty("source").GetString());
        }

        [Fact]
        public async Task A_supplier_without_activities_returns_no_latest_activity()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(RespondPerTable(SupplierResponses.OtherProspectInformation, SupplierResponses.Empty));

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Null, body.GetProperty("otherProspectInformation").GetProperty("latestActivity").ValueKind);
        }

        [Fact]
        public async Task An_unknown_supplier_returns_four_hundred_four_without_querying_activities()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(RespondPerTable(SupplierResponses.Empty, SupplierResponses.LatestActivity));
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("supplier_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            var query = Assert.Single(fixture.Quickbase.Queries.Skip(before));
            Assert.Equal("bqrcgnatz", query.TableId);
        }

        [Fact]
        public async Task A_quickbase_failure_on_the_activities_query_returns_five_hundred_two()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(query => query.TableId == "bqrcgnatz"
                ? SupplierResponses.OtherProspectInformation
                : throw new QuickbaseApiException("Quickbase is down.", 503));

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.Equal("quickbase_unavailable", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_a_caller_without_supplier_access(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task A_user_who_is_not_a_platform_administrator_gets_four_hundred_three_and_no_query_runs()
        {
            var (uid, _) = await fixture.SeedUserAsync();
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }
    }
}
