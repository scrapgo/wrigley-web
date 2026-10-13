// A supplier's contacts: a list from the Contacts table, the main contact first.
namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class ContactsSpecs
{
    private const string Path = "/17511/contacts";

    public class Given_a_supplier_reader(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Sends_the_contacts_query_and_returns_the_main_contact_first()
        {
            var (token, organizationId) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);
            fixture.Quickbase.RespondWith(SupplierResponses.Contacts);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var query = fixture.Quickbase.Queries.Last();
            Assert.Equal("br8imsxs2", query.TableId);
            Assert.Equal([3, 29, 20, 18, 25, 22, 15, 17, 21, 46], query.Select);
            Assert.Equal("{29.EX.'17511'}", query.Where);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(17511, body.GetProperty("supplierRecordId").GetInt32());
            var contacts = body.GetProperty("contacts").EnumerateArray().ToList();

            // Main contact first, then by last name ignoring case: Esteves (main), brown, Smith.
            Assert.Equal([42, 55, 61], contacts.Select(c => c.GetProperty("recordId").GetInt32()));

            var main = contacts.First();
            Assert.Equal(17511, main.GetProperty("relatedSupplier").GetInt32());
            Assert.Equal("Esteves", main.GetProperty("lastName").GetString());
            Assert.Equal("David", main.GetProperty("firstName").GetString());
            Assert.Equal("Owner", main.GetProperty("title").GetString());
            Assert.Equal("(321) 356-4622", main.GetProperty("phone").GetString());
            Assert.Equal("(321) 555-0199", main.GetProperty("cell").GetString());
            Assert.Equal("pipoe720@hotmail.com", main.GetProperty("email").GetString());
            Assert.Equal("Prefers texts.", main.GetProperty("notes").GetString());
            Assert.True(main.GetProperty("isMain").GetBoolean());

            // Empty text and missing fields come back as null.
            var smith = contacts.Last();
            Assert.Equal(JsonValueKind.Null, smith.GetProperty("phone").ValueKind);
            Assert.Equal(JsonValueKind.Null, smith.GetProperty("notes").ValueKind);
            Assert.False(smith.GetProperty("isMain").GetBoolean());
        }

        [Fact]
        public async Task A_supplier_without_contacts_returns_an_empty_list()
        {
            var (token, organizationId) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);
            fixture.Quickbase.RespondWith(SupplierResponses.Empty);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contacts").GetArrayLength());
        }

        [Fact]
        public async Task Is_platform_wide_for_platform_administrators()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.Contacts);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers" + Path, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    public class Given_a_caller_without_supplier_access(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task A_member_without_the_permission_gets_four_hundred_three_and_no_query_runs()
        {
            var (uid, _, organizationId) = await fixture.SeedMemberAsync();
            await fixture.EntitleAsync(organizationId, DownstreamApplication.Id, DownstreamApplication.SuppliersModuleId);
            var before = fixture.Quickbase.Queries.Count;

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, Path), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }

        [Fact]
        public async Task An_organization_reader_gets_four_hundred_three_on_the_platform_route()
        {
            var (token, _) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers" + Path, token);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
