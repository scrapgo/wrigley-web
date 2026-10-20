// INTERNAL_HD_ALLOWLIST is read as a JSON array (appsettings) or a comma-separated
// string (the Cloud Run env var). Pure configuration specs: no database needed.
using System.Text;
using Microsoft.Extensions.Configuration;
using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Modules.Identity.Infrastructure.Users;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class InternalHdAllowlistSpecs
{
    private static InternalHdAllowlistOptions FromJson(string json)
    {
        var configuration = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();
        return new InternalHdAllowlistOptions { HostedDomains = InternalHdAllowlistConfiguration.Read(configuration) };
    }

    private static InternalHdAllowlistOptions FromString(string? value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(value is null ? [] : [new(InternalHdAllowlistOptions.ConfigurationKey, value)])
            .Build();
        return new InternalHdAllowlistOptions { HostedDomains = InternalHdAllowlistConfiguration.Read(configuration) };
    }

    public class Given_a_json_array
    {
        [Fact]
        public void Scrapgo_dot_com_is_internal() =>
            Assert.True(FromJson("""{ "INTERNAL_HD_ALLOWLIST": ["scrapgo.com"] }""").Contains("scrapgo.com"));

        [Fact]
        public void Every_item_is_internal()
        {
            var allowlist = FromJson("""{ "INTERNAL_HD_ALLOWLIST": ["example.com", " scrapgo.com "] }""");

            Assert.Equal(["example.com", "scrapgo.com"], allowlist.HostedDomains.Order());
        }

        [Fact]
        public void An_empty_array_makes_nobody_internal() =>
            Assert.Empty(FromJson("""{ "INTERNAL_HD_ALLOWLIST": [] }""").HostedDomains);
    }

    public class Given_a_comma_separated_string
    {
        [Fact]
        public void Scrapgo_dot_com_is_internal() =>
            Assert.True(FromString("scrapgo.com").Contains("scrapgo.com"));

        [Fact]
        public void Every_entry_is_internal_and_blanks_are_dropped()
        {
            var allowlist = FromString("example.com, scrapgo.com,,");

            Assert.Equal(["example.com", "scrapgo.com"], allowlist.HostedDomains.Order());
        }

        [Fact]
        public void The_same_string_in_json_is_read_too() =>
            Assert.True(FromJson("""{ "INTERNAL_HD_ALLOWLIST": "example.com,scrapgo.com" }""").Contains("scrapgo.com"));
    }

    public class Given_an_env_var_over_an_appsettings_array
    {
        private static InternalHdAllowlistOptions Layered(string envValue)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes("""{ "INTERNAL_HD_ALLOWLIST": ["scrapgo.com"] }""")))
                .AddInMemoryCollection([new(InternalHdAllowlistOptions.ConfigurationKey, envValue)])
                .Build();
            return new InternalHdAllowlistOptions { HostedDomains = InternalHdAllowlistConfiguration.Read(configuration) };
        }

        [Fact]
        public void The_env_var_wins() =>
            Assert.Equal(["example.com"], Layered("example.com").HostedDomains);

        [Fact]
        public void A_blank_env_var_leaves_the_array() =>
            Assert.True(Layered("").Contains("scrapgo.com"));
    }

    public class Given_a_blank_allowlist
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Nobody_is_internal(string? value)
        {
            var allowlist = FromString(value);

            Assert.Empty(allowlist.HostedDomains);
            Assert.False(allowlist.Contains("scrapgo.com"));
        }
    }

    public class Given_mixed_case
    {
        [Fact]
        public void A_mixed_case_string_entry_matches_a_lower_case_hd() =>
            Assert.True(FromString("ScrapGo.COM").Contains("scrapgo.com"));

        [Fact]
        public void A_mixed_case_array_item_matches_a_mixed_case_hd() =>
            Assert.True(FromJson("""{ "INTERNAL_HD_ALLOWLIST": ["SCRAPGO.com"] }""").Contains("ScrapGo.com"));
    }
}
