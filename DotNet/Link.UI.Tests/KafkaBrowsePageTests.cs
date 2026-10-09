using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Link.UI.Tests;

public class KafkaBrowsePageTests
{
    [Fact]
    public void Query_keeps_a_message_seek_and_drops_it_on_the_other_tabs()
    {
        var raw = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["view"] = "messages",
            ["topic"] = "ResourcesAcquired",
            ["mode"] = "from-offset",
            ["partition"] = new StringValues(["0", "2, 4"]),
            ["offset"] = "15",
            ["timestamp"] = "1710000000000",
            ["limit"] = "0",
            ["key"] = "fac-1|pat",
            ["headerName"] = "X-Correlation-Id",
            ["headerValue"] = "abc def",
            ["record"] = "0:120"
        });

        var query = ThroughputKafkaPageQuery.From(raw, "/Operations");
        query.View.Should().Be(ThroughputKafkaPageQuery.Messages);
        query.BrowseMode.Should().Be("from-offset");
        query.BrowsePartitions.Should().Equal(0, 2, 4);
        query.BrowseOffset.Should().Be(15);
        query.BrowseTimestamp.Should().Be(1710000000000);
        query.BrowseLimit.Should().Be(0);
        query.BrowseKey.Should().Be("fac-1pat");
        query.BrowseHeaderName.Should().Be("X-Correlation-Id");
        query.BrowseHeaderValue.Should().Be("abc def");
        query.OpenRecord.Should().Be("0:120");

        var href = query.Href();
        href.Should().Contain("view=messages");
        href.Should().Contain("mode=from-offset");
        href.Should().Contain("partition=0");
        href.Should().Contain("partition=2");
        href.Should().Contain("partition=4");
        href.Should().Contain("offset=15");
        href.Should().Contain("limit=0");
        href.Should().Contain("record=0%3A120");
        query.ExportHref().Should().StartWith("/Operations/Kafka/messages/export?");
        query.ExportHref().Should().NotContain("record=");

        var overview = query.Href(view: ThroughputKafkaPageQuery.Overview, page: 1);
        overview.Should().Contain("view=overview");
        overview.Should().NotContain("mode=");
        overview.Should().NotContain("partition=");
        overview.Should().NotContain("record=");
    }

    [Fact]
    public void Messages_tab_explains_the_read_and_puts_the_result_by_the_buttons()
    {
        var root = RepoRoot();
        var page = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Messages.cshtml"));
        var shell = File.ReadAllText(Path.Combine(root, "Views", "Operations", "Kafka.cshtml"));
        page.Should().Contain("does not commit offsets");
        page.Should().Contain("Topic family");
        page.Should().Contain("<legend class=\"float-none w-auto px-2 h6\">Where to read</legend>");
        page.Should().Contain("<legend class=\"float-none w-auto px-2 h6\">What to keep</legend>");
        page.Should().Contain("id=\"kafka-browse-result\"");
        page.Should().Contain("Blocking reasons");
        page.Should().Contain("KafkaBrowseText.FacilityHref");
        page.Should().Contain("KafkaBrowseText.ReportHref");
        page.Should().Contain("ValuePretty");
        page.Should().Contain("Export JSON");
        page.Should().Contain("btn btn-au-execute");
        page.Should().Contain("btn btn-au-link");
        page.Should().Contain("btn btn-warning");
        page.Should().NotContain("btn-primary");
        page.Should().NotContain("btn-outline-");
        IndexOf(page, "id=\"kafka-browse-result\"").Should().BeLessThan(IndexOf(page, ">Fetch</button>"));
        shell.Should().Contain("ThroughputKafkaPageQuery.Messages");
        shell.Should().Contain(">Messages</a>");
    }

    [Fact]
    public void Context_links_open_the_tenant_and_the_report()
    {
        KafkaBrowseText.FacilityHref("11111111-1111-1111-1111-111111111111")
            .Should().Be("/Tenants/View/11111111-1111-1111-1111-111111111111");
        KafkaBrowseText.ReportHref("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222")
            .Should().Be("/Tenants/Report/11111111-1111-1111-1111-111111111111?reportId=22222222-2222-2222-2222-222222222222");
        KafkaBrowseText.FacilityHref(" ").Should().BeEmpty();
        KafkaBrowseText.ReportHref("fac", null).Should().BeEmpty();
        KafkaBrowseText.Verdict(null, "That topic is not in the catalog.").Should().Be("This read was refused.");
        KafkaBrowseText.NextStep(null, "That topic is not in the catalog.").Should().Contain("fetch again");
        var capped = new KafkaBrowsePage { Metadata = new KafkaBrowseMetadata { CapHit = true, Returned = 1 } };
        KafkaBrowseText.Tone(capped, null).Should().Be("alert-warning");
        KafkaBrowseText.Badge(capped, null).Should().Be("Capped");
    }

    [Fact]
    public void Fixture_returns_a_family_a_page_and_a_refusal()
    {
        var fixture = Fixture();
        var family = fixture.Family("ResourcesAcquired");
        family.Value.Should().NotBeNull();
        family.Value!.Main.Should().Be("ResourcesAcquired");
        var main = family.Value.Members.Single(member => member.Kind == KafkaBrowseAllowList.KindMain);
        main.Exists.Should().BeTrue();
        main.Partitions.Should().Be(3);
        main.HighWatermarkSum.Should().Be(120);
        main.Lag.Should().Be(12);
        family.Value.Members.Single(member => member.Kind == KafkaBrowseAllowList.KindError).Exists.Should().BeTrue();
        family.Value.Members.Single(member => member.Topic == "ResourcesAcquired-Retry").Exists.Should().BeFalse();

        var page = fixture.Messages("ResourcesAcquired", "newest", [], null, null, 25, "", "", "");
        page.Value!.Records.Should().NotBeEmpty();
        page.Value.Records[0].Link.FacilityId.Should().Be("11111111-1111-1111-1111-111111111111");
        page.Value.Records[0].Link.ReportId.Should().Be("22222222-2222-2222-2222-222222222222");
        page.Value.Records[0].ValuePretty.Should().Contain("\"resourceType\"");
        page.Value.Metadata.CapHit.Should().BeFalse();

        var capped = fixture.Messages("ResourcesAcquired", "newest", [], null, null, 25, "cap", "", "");
        capped.Value!.Metadata.CapHit.Should().BeTrue();
        capped.Value.Metadata.Truncated.Should().BeTrue();

        fixture.Messages("NotAPipelineTopic", "newest", [], null, null, 25, "", "", "").Error.Should().NotBeNullOrWhiteSpace();
        fixture.Messages("ResourcesAcquired", "newest", [], null, null, 51, "", "", "").Error.Should().Contain("1 to 50");
        fixture.Messages("ResourcesAcquired", "from-offset", [], null, null, 25, "", "", "").Error.Should().Contain("offset");
        fixture.Family("__consumer_offsets").Error.Should().NotBeNullOrWhiteSpace();
    }

    private static KafkaOpsFixture Fixture()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LinkUi:KafkaOpsFixture"] = "true" })
            .Build();
        return new KafkaOpsFixture(configuration, new ContentRoot(RepoRoot()));
    }

    private static int IndexOf(string text, string needle)
    {
        var index = text.IndexOf(needle, StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0, needle);
        return index;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }

    private sealed class ContentRoot(string path) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = path;
        public string WebRootPath { get; set; } = path;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
