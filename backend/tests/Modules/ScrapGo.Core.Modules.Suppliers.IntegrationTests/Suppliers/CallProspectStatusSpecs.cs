// A supplier's call and prospect status: last call result and objection
// (dropdowns), call back date, prospect status and notes.
using ScrapGo.Core.Modules.Suppliers.Application.Suppliers;
using ScrapGo.Core.Modules.Suppliers.Infrastructure.Quickbase;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class CallProspectStatusSpecs
{
    public class Given_a_supplier_reader(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task Sends_the_call_status_query_and_maps_every_field()
        {
            var (token, organizationId) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);
            fixture.Quickbase.RespondWith(SupplierResponses.CallProspectStatus);

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, "/17511/call-prospect-status"), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var query = fixture.Quickbase.Queries.Last();
            Assert.Equal("bqrcgnatz", query.TableId);
            Assert.Equal([3, 197, 192, 193, 236, 181, 238, 97], query.Select);
            Assert.Equal("{3.EX.'17511'}", query.Where);

            var status = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("callProspectStatus");
            Assert.Equal(17511, status.GetProperty("recordId").GetInt32());
            Assert.Equal("Yes", status.GetProperty("contactWithDecisionMakerMade").GetString());
            Assert.Equal("Prospect", status.GetProperty("prospectStatus").GetString());
            Assert.Equal("NoAnswerVoiceMail", status.GetProperty("lastCallResult").GetString());
            Assert.Equal("PaymentTerms", status.GetProperty("supplierObjection").GetString());
            Assert.Equal(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), status.GetProperty("callBackDate").GetDateTimeOffset());
            Assert.Equal("Wants Net 5 instead of Net 10.", status.GetProperty("objectionExplained").GetString());
            Assert.Equal("Spoke to David; call back next week.", status.GetProperty("callNotes").GetString());
        }

        [Fact]
        public async Task Unknown_dropdown_text_and_empty_dates_come_back_as_null()
        {
            var (token, organizationId) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);
            fixture.Quickbase.RespondWith(SupplierResponses.CallProspectStatusUnknownChoices);

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, "/9583/call-prospect-status"), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var status = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("callProspectStatus");
            Assert.Equal(JsonValueKind.Null, status.GetProperty("lastCallResult").ValueKind);
            Assert.Equal(JsonValueKind.Null, status.GetProperty("supplierObjection").ValueKind);
            Assert.Equal(JsonValueKind.Null, status.GetProperty("callBackDate").ValueKind);
        }

        [Fact]
        public async Task An_unknown_record_returns_four_hundred_four()
        {
            var (token, organizationId) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);
            fixture.Quickbase.RespondWith(SupplierResponses.Empty);

            var response = await fixture.SendAsync(
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, "/99999999/call-prospect-status"), token);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("supplier_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task Is_platform_wide_for_platform_administrators()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            fixture.Quickbase.RespondWith(SupplierResponses.CallProspectStatus);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers/17511/call-prospect-status", fixture.CreateToken(uid));

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
                HttpMethod.Get, SuppliersSpecFixture.SuppliersPath(organizationId, "/17511/call-prospect-status"), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(before, fixture.Quickbase.Queries.Count);
        }

        [Fact]
        public async Task An_organization_reader_gets_four_hundred_three_on_the_platform_route()
        {
            var (token, _) = await fixture.SeedSupplierReaderAsync(DownstreamApplication.SuppliersModuleId);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/suppliers/17511/call-prospect-status", token);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class The_dropdowns
    {
        public static TheoryData<LastCallResult> LastCallResultValues => [.. Enum.GetValues<LastCallResult>()];

        public static TheoryData<SupplierObjection> SupplierObjectionValues => [.. Enum.GetValues<SupplierObjection>()];

        [Theory]
        [MemberData(nameof(LastCallResultValues))]
        public void Every_last_call_result_round_trips_through_its_quickbase_text(LastCallResult value)
        {
            var text = QuickbaseDropdowns.ToQuickbase(LastCallResults.Catalog, value);
            Assert.True(QuickbaseDropdowns.TryFromQuickbase(LastCallResults.Catalog, text, out var parsed));
            Assert.Equal(value, parsed);
        }

        [Theory]
        [MemberData(nameof(SupplierObjectionValues))]
        public void Every_supplier_objection_round_trips_through_its_quickbase_text(SupplierObjection value)
        {
            var text = QuickbaseDropdowns.ToQuickbase(SupplierObjections.Catalog, value);
            Assert.True(QuickbaseDropdowns.TryFromQuickbase(SupplierObjections.Catalog, text, out var parsed));
            Assert.Equal(value, parsed);
        }

        [Theory]
        [InlineData("Past ScrapGo Issues", SupplierObjection.PastScrapGoIssues)]
        [InlineData("Sells to Our Consumer", SupplierObjection.SellsToOurConsumer)]
        public void Supplier_objections_read_the_confirmed_quickbase_text(string text, SupplierObjection expected)
        {
            Assert.True(QuickbaseDropdowns.TryFromQuickbase(SupplierObjections.Catalog, text, out var parsed));
            Assert.Equal(expected, parsed);
        }

        [Theory]
        [InlineData("No Answer - Voice Mail", LastCallResult.NoAnswerVoiceMail)]
        [InlineData("can\u2019t fill truck load", LastCallResult.CantFillTruckLoad)]
        [InlineData(" PO  pending ", LastCallResult.PoPending)]
        public void Reading_ignores_case_spacing_and_curly_apostrophes(string text, LastCallResult expected)
        {
            Assert.True(QuickbaseDropdowns.TryFromQuickbase(LastCallResults.Catalog, text, out var parsed));
            Assert.Equal(expected, parsed);
        }
    }

    public class The_options_endpoints(SuppliersSpecFixture fixture) : IClassFixture<SuppliersSpecFixture>
    {
        [Fact]
        public async Task List_every_option_in_order()
        {
            var (uid, _) = await fixture.SeedUserAsync();
            var token = fixture.CreateToken(uid);

            var lastCall = await ReadOptionsAsync(await fixture.SendAsync(HttpMethod.Get, "/api/suppliers/last-call-results", token));
            var objections = await ReadOptionsAsync(await fixture.SendAsync(HttpMethod.Get, "/api/suppliers/supplier-objections", token));

            Assert.Equal(14, lastCall.Count);
            Assert.Equal(("BadNumber", "Bad Number"), lastCall[0]);
            Assert.Equal(("PoPending", "PO Pending"), lastCall[^1]);
            Assert.Equal(22, objections.Count);
            Assert.Equal(("Hot", "Hot"), objections[0]);
            Assert.Equal(("TooFar", "Too Far"), objections[^1]);
        }

        [Fact]
        public async Task An_anonymous_caller_gets_four_hundred_one()
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/suppliers/last-call-results")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/suppliers/supplier-objections")).StatusCode);
        }

        private static async Task<List<(string?, string?)>> ReadOptionsAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return [.. (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
                .Select(o => (o.GetProperty("value").GetString(), o.GetProperty("label").GetString()))];
        }
    }
}
