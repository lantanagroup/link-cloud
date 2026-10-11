using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.UI.Controllers;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class KafkaDashboardWidgetTests
{
    private static string Ui(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "Link.UI", "Views")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine([dir!, "Link.UI", .. parts]));
    }

    [Fact]
    public void History_ring_is_bounded_spaced_and_derives_rates()
    {
        var ring = new KafkaThroughputHistory(capacity: 5, minSpacing: TimeSpan.FromSeconds(30));
        var t0 = DateTimeOffset.UnixEpoch;
        ring.Record(t0, [new KafkaTopicSample("A", 0, 0, 0, 10)]).Should().BeTrue();
        ring.Record(t0.AddSeconds(10), [new KafkaTopicSample("A", 999, 0, 0, 10)]).Should().BeFalse("samples closer than the spacing are dropped");
        for (var i = 1; i <= 10; i++)
            ring.Record(t0.AddMinutes(i), [new KafkaTopicSample("A", i * 600, i, i * 2, 10)]);
        ring.Count.Should().Be(5);
        var snap = ring.Snapshot(3);
        snap.Points.Should().HaveCount(4);
        snap.Points[^1].InPerSecond.Should().Be(10);
        snap.Points[^1].OutPerSecond.Should().Be(10);
        snap.Busiest.Should().ContainSingle(row => row.Topic == "A");
        snap.Landed.Should().ContainSingle().Which.Error.Should().Be(8);
    }

    [Fact]
    public void History_ring_drops_frames_older_than_the_window()
    {
        var ring = new KafkaThroughputHistory(capacity: 500, minSpacing: TimeSpan.FromSeconds(30));
        var t0 = DateTimeOffset.UnixEpoch;
        for (var i = 0; i <= 180; i++)
            ring.Record(t0.AddMinutes(i), [new KafkaTopicSample("A", i, 0, 0, 0)]);
        ring.Count.Should().BeLessThanOrEqualTo(61);
        new KafkaThroughputHistory().Snapshot().Points.Should().BeEmpty();
    }

    [Fact]
    public void Pipeline_follows_the_catalog_order_and_colours_by_lag()
    {
        var steps = KafkaDashboardRules.Pipeline(
        [
            new KafkaTopicRow { Topic = "SubmitPayload", TotalLag = 0 },
            new KafkaTopicRow { Topic = "ResourcesAcquired", TotalLag = 5 },
            new KafkaTopicRow { Topic = "ReportScheduled", TotalLag = 5000 },
            new KafkaTopicRow { Topic = "Service-Healthcheck" },
            new KafkaTopicRow { Topic = "PatientEvent", LagKnown = false }
        ]);
        steps.Select(step => step.Topic).Should().Equal("ReportScheduled", "PatientEvent", "ResourcesAcquired", "SubmitPayload");
        steps.Select(step => step.Tone).Should().Equal("bad", "unknown", "warn", "ok");
        steps[0].Href.Should().Be("/Operations/Kafka/Topics/ReportScheduled/Summary");
        KafkaTopicCatalog.PipelineOrder[0].Should().Be("ReportScheduled");
    }

    [Fact]
    public void Leader_donut_uses_the_shared_palette_without_black_or_dark_grey()
    {
        var cluster = new ClusterSnapshot { Brokers = Enumerable.Range(0, 14).Select(i => new BrokerSnapshot { Id = i, LeaderCount = 20 - i }).ToList() };
        var slices = KafkaDashboardRules.Leaders(cluster);
        slices.Should().HaveCount(ReportManifestRules.DonutColors.Length);
        slices[^1].Name.Should().Be("Other brokers");
        foreach (var color in slices.Select(slice => slice.Color))
        {
            (ReportManifestRules.DonutColors.Contains(color) || color == ReportManifestRules.DonutOtherColor).Should().BeTrue();
            new[] { "#000", "#000000", "#111", "#212529", "#343a40", "#333" }.Should().NotContain(color);
        }
    }

    [Fact]
    public void Broker_cards_are_capped_and_show_disk_only_when_reported()
    {
        var cluster = new ClusterSnapshot
        {
            Brokers = Enumerable.Range(0, 40).Select(i => new BrokerSnapshot { Id = i, State = "up", LogDirBytes = i == 0 ? 2_500_000_000 : -1 }).ToList()
        };
        var cards = KafkaDashboardRules.Brokers(cluster);
        cards.Should().HaveCount(KafkaDashboard.MaxBrokerCards);
        cards[0].Disk.Should().Be("2.5 GB");
        cards[1].Disk.Should().BeEmpty();
        KafkaDashboardRules.Spark([1.0]).Should().BeEmpty("a sparkline hides without history");
    }

    [Fact]
    public void Every_widget_is_its_own_partial_with_its_own_endpoint_and_refresh_id()
    {
        KafkaWidgetsController.Partials.Keys.Should().BeEquivalentTo(["pipeline", "brokers", "leaders", "errors", "throughput", "summary"]);
        foreach (var (name, partial) in KafkaWidgetsController.Partials)
        {
            var view = Ui("Views", "Shared", partial.Replace('/', Path.DirectorySeparatorChar) + ".cshtml");
            view.Should().Contain("id=\"kafka-widget-" + name + "\"");
            view.Should().Contain("@model KafkaDashboard");
            view.Should().NotContain("btn-au-neutral");
            view.Should().NotContain("DevOps");
        }
        Ui("Views", "Home", "Index.cshtml").Should().Contain("id=\"kafka-widget-summary\"").And.Contain("data-au-refresh-url=\"/Operations/Kafka/widgets/summary\"");
    }

    [Fact]
    public void Overview_uses_the_widgets_and_the_attention_table_shows_item_with_a_kind_badge()
    {
        var overview = Ui("Views", "Operations", "_Overview.cshtml");
        foreach (var partial in new[] { "_KafkaPipeline", "_KafkaBrokerHealth", "_KafkaLeaderDonut", "_KafkaThroughput", "_KafkaErrorRetry" })
            overview.Should().Contain("Kafka/" + partial);
        overview.Should().Contain("<th class=\"lu-flex\">Item</th>");
        overview.Should().NotContain(">What</th>");
        overview.Should().NotContain("<th>Kind</th>");
        overview.Should().Contain("lu-kind-badge\">@item.Kind");
        Ui("Views", "Shared", "Kafka", "_KafkaThroughput.cshtml").Should().Contain("@if (Model.HasHistory)");
        Ui("Views", "Shared", "Kafka", "_KafkaErrorRetry.cshtml").Should().Contain("KafkaConsoleRoutes.Messages");
    }
}
