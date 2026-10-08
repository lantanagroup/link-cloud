using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Link.UI.Tests;

public class KafkaOpsPageTests
{
    [Fact]
    public void Query_defaults_to_overview_and_rejects_unknown_views()
    {
        ThroughputKafkaPageQuery.From(new QueryCollection(), null).View.Should().Be(ThroughputKafkaPageQuery.Overview);

        var unknown = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["view"] = "plan"
        });
        var query = ThroughputKafkaPageQuery.From(unknown, "/Operations");
        query.View.Should().Be(ThroughputKafkaPageQuery.Overview);
        query.Href().Should().Contain("view=overview");
        query.ReturnUrl.Should().Be("/Operations");
    }

    [Fact]
    public void Query_keeps_topic_group_broker_and_test_groups_in_the_query_string()
    {
        var raw = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["view"] = "consumers",
            ["group"] = "Report",
            ["topic"] = "ReportScheduled",
            ["broker"] = "2",
            ["tests"] = "1",
            ["advanced"] = "1",
            ["keyClass"] = "Patient",
            ["pageSize"] = "10"
        });
        var query = ThroughputKafkaPageQuery.From(raw, null);
        query.View.Should().Be(ThroughputKafkaPageQuery.Consumers);
        query.Tests.Should().BeTrue();
        query.Advanced.Should().BeTrue();
        query.PageSize.Should().Be(10);
        var href = query.Href();
        href.Should().Contain("view=consumers");
        href.Should().Contain("group=Report");
        href.Should().Contain("topic=ReportScheduled");
        href.Should().Contain("broker=2");
        href.Should().Contain("tests=1");
        href.Should().Contain("keyClass=Patient");
    }

    [Fact]
    public void Kafka_views_follow_the_shared_page_rules()
    {
        var root = ProjectRoot();
        var views = Directory.EnumerateFiles(Path.Combine(root, "Views", "Operations"), "*.cshtml");
        var text = string.Join("\n", views.Select(File.ReadAllText));
        text.Should().Contain("lu-section-nav");
        text.Should().Contain("Overview");
        text.Should().Contain("Consumers");
        text.Should().Contain("Brokers");
        text.Should().Contain("Advanced: add partitions");
        text.Should().Contain("Type the topic name");
        text.Should().Contain("LinkUiTime.Display");
        text.Should().Contain("_LabeledId");
        text.Should().Contain("_BackButton");
        text.Should().Contain("aria-label");
        text.Should().Contain("data-au-refresh");
        text.Should().Contain("returnUrl");
        text.Should().NotContain("left unchanged");
        text.Should().NotContain("btn-outline-");
        text.Should().NotContain("btn-primary");
        text.Should().NotContain("nav-tabs");
        foreach (var hex in new[] { "#0d6efd", "#0dcaf0", "#4da3ff", "#198754" })
            text.Should().NotContain(hex);

        var script = File.ReadAllText(Path.Combine(root, "wwwroot", "js", "kafka-ops.js"));
        script.Should().NotContain("#0d6efd");
        script.Should().Contain("#28a745");
        script.Should().Contain("luPaintTimes");
    }

    [Fact]
    public void Kafka_status_pills_are_not_blue()
    {
        StatusPills.ForKafka("Stable").Should().Be("au-badge-success");
        StatusPills.ForKafka("Converging").Should().Be("au-badge-active");
        StatusPills.ForKafka("Failed").Should().Be("au-badge-danger");
        StatusPills.ForKafka("Empty").Should().Be("au-badge-warning");
        StatusPills.ForKafka(null).Should().Be("au-badge-muted");
    }

    [Fact]
    public void Recorded_fixture_covers_each_panel()
    {
        var root = ProjectRoot();
        var json = File.ReadAllText(Path.Combine(root, "Fixtures", "kafka-ops.json"));
        json.Should().Contain("\"provider\": \"Disabled\"");
        json.Should().Contain("ResourcesAcquired");
        json.Should().Contain("ReadyToAcquire");
        json.Should().Contain("DataAcquisitionRequested");
        json.Should().Contain("Normalization");
        json.Should().Contain("broker-0");
        json.Should().Contain("22222222-2222-2222-2222-222222222222");
        json.Should().Contain("33333333-3333-3333-3333-333333333333");
    }

    private static string ProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }
}
