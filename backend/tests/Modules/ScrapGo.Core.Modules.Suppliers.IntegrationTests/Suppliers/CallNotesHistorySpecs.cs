// A supplier's call notes history: a list from the Call Notes table, newest first.
namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class CallNotesHistorySpecs
{
    private const string Path = "/17511/call-notes-history";

    public class Given_a_supplier_reader(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Sends_the_call_notes_query_and_returns_the_notes_newest_first()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.CallNotes);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var query = fixture.Quickbase.Queries.Last();
            Assert.Equal("br8tgw3tp", query.TableId);
            Assert.Equal([3, 1, 17, 19], query.Select);
            Assert.Equal("{19.EX.'17511'}", query.Where);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(17511, body.GetProperty("supplierRecordId").GetInt32());
            var notes = body.GetProperty("notes").EnumerateArray().ToList();
            Assert.Equal([845, 822, 801], notes.Select(n => n.GetProperty("recordId").GetInt32()));

            var newest = notes.First();
            Assert.Equal(new DateTimeOffset(2026, 10, 6, 18, 30, 0, TimeSpan.Zero), newest.GetProperty("dateCreated").GetDateTimeOffset());
            Assert.Equal("Spoke to David; wants Net 5.", newest.GetProperty("notes").GetString());
            Assert.Equal(17511, newest.GetProperty("relatedSupplier").GetInt32());

            // Empty note text comes back as null.
            Assert.Equal(JsonValueKind.Null, notes[1].GetProperty("notes").ValueKind);
        }

        [Fact]
        public async Task A_supplier_without_notes_returns_an_empty_list()
        {
            var token = await fixture.SeedSupplierReaderAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.Empty);

            var response = await fixture.SendAsync(HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(Path), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("notes").GetArrayLength());
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
