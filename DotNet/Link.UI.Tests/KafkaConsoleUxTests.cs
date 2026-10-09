using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Link.UI.Tests;

public class KafkaConsoleUxTests
{
    [Fact]
    public void Attention_lists_lag_and_leaves_a_quiet_topic_alone()
    {
        KafkaAttention.Topic(new KafkaTopicRow { Topic = "Quiet", FullIsr = true, LagKnown = true }).Should().BeFalse();

        var behind = new KafkaTopicRow { Topic = "ResourcesAcquired", TotalLag = 12, LagKnown = true, FullIsr = true };
        KafkaAttention.Topic(behind).Should().BeTrue();
        KafkaAttention.Join(KafkaAttention.TopicReasons(behind)).Should().Be("Lag is 12.");

        var unknown = new KafkaTopicRow { LagKnown = false, FullIsr = false, LeadersSkewed = true };
        KafkaAttention.Join(KafkaAttention.TopicReasons(unknown)).Should().Contain("The ISR is short.");
        KafkaAttention.Join(KafkaAttention.TopicReasons(unknown)).Should().Contain("Lag is unknown.");
    }

    [Fact]
    public void Attention_flags_a_group_with_lag_and_a_broker_that_is_not_up()
    {
        KafkaAttention.Group(new KafkaGroupRow { GroupId = "Report", State = "Stable", Catalogued = true }).Should().BeFalse();
        var behind = new KafkaGroupRow { GroupId = "Normalization", State = "Stable", TotalLag = 12, Catalogued = true };
        KafkaAttention.Join(KafkaAttention.GroupReasons(behind)).Should().Be("Lag is 12.");

        var empty = new KafkaGroupRow { GroupId = "Idle", State = "Empty", Catalogued = true };
        KafkaAttention.Group(empty).Should().BeTrue();

        KafkaAttention.Broker(new BrokerSnapshot { Id = 0, State = "up" }).Should().BeFalse();
        KafkaAttention.Broker(new BrokerSnapshot { Id = 1, State = "offline" }).Should().BeTrue();
        KafkaAttention.Broker(new BrokerSnapshot { Id = 2, State = "" }).Should().BeTrue();
    }

    [Fact]
    public void Attention_flags_an_offline_leader_a_short_isr_and_lag()
    {
        KafkaAttention.Partition(0, "0, 1, 2", "0, 1, 2", [0, 0]).Should().BeFalse();
        KafkaAttention.Partition(-1, "0, 1", "0, 1", [0]).Should().BeTrue();
        KafkaAttention.Partition(0, "0, 1, 2", "0, 1", [0]).Should().BeTrue();
        KafkaAttention.Partition(0, "0, 1, 2", "0, 1, 2", [4]).Should().BeTrue();
        KafkaAttention.CommittedPartition(0, true).Should().BeFalse();
        KafkaAttention.CommittedPartition(0, false).Should().BeTrue();
    }

    [Fact]
    public void Kafka_tabs_lead_with_an_explanation_and_keep_previews_beside_the_buttons()
    {
        var root = RepoRoot();
        var overview = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Overview.cshtml"));
        var topic = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Topic.cshtml"));
        var consumers = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Consumers.cshtml"));
        var brokers = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Brokers.cshtml"));
        var home = File.ReadAllText(Path.Combine(root, "Views", "Operations", "Index.cshtml"));

        overview.Should().Contain("Read this before changing anything.");
        overview.Should().Contain("Topics that need attention");
        overview.Should().Contain("KafkaAttention.Topic");
        overview.Should().Contain("class=\"lu-kafka-fold\"");
        overview.Should().Contain("Full topic table");

        topic.Should().Contain("Preview it first.");
        topic.Should().Contain("<legend class=\"float-none w-auto px-2 h6\">What to change</legend>");
        topic.Should().Contain("<legend class=\"float-none w-auto px-2 h6\">Request only</legend>");
        topic.Should().Contain("A preview does not read these. Submit does.");
        IndexOf(topic, "id=\"kafka-partition-result\"").Should().BeLessThan(IndexOf(topic, ">Preview increase</span>"));
        topic.Should().Contain("data-lu-result=\"kafka-partition\"");
        consumers.Should().Contain("data-lu-result=\"kafka-replica\"");
        brokers.Should().Contain("data-lu-result=\"kafka-broker\"");

        consumers.Should().Contain("Add or remove members only after a preview.");
        consumers.Should().Contain("id=\"kafka-replica-result\"");
        consumers.Should().Contain("A preview does not read this. Add and Remove do.");
        IndexOf(consumers, "id=\"kafka-replica-result\"").Should().BeLessThan(IndexOf(consumers, ">Preview add</span>"));

        brokers.Should().Contain("A preview plans the partitions and changes nothing.");
        brokers.Should().Contain("<legend class=\"float-none w-auto px-2 h6\">Request only</legend>");
        IndexOf(brokers, "id=\"kafka-broker-result\"").Should().BeLessThan(IndexOf(brokers, ">Preview decommission</span>"));

        home.Should().Contain("changes that start as a preview");
        home.Should().Contain("Open Kafka");
        home.Should().Contain("btn btn-au-link");
    }

    [Fact]
    public void Partition_window_pages_worst_first_and_caps_the_hot_rows()
    {
        var rows = Enumerable.Range(0, 200).Select(id => new KafkaPartitionDetail
        {
            Partition = id,
            Leader = id == 7 ? -1 : id % 5,
            Replicas = "0, 1, 2",
            InSync = id == 3 ? "0" : "0, 1, 2",
            Lag = id < 12 || id == 50
                ? [new KafkaPartitionLagRow { GroupId = "Normalization", Lag = id == 50 ? 900 : id + 1 }]
                : []
        }).ToList();

        var first = KafkaWindows.Topic(rows, 1, 10);
        first.PageSize.Should().Be(10);
        first.Total.Should().Be(200);
        first.Pages.Should().Be(20);
        first.Rows.Should().HaveCount(10);
        first.Rows[0].Partition.Should().Be(50);
        first.Hot.Should().BeGreaterThan(KafkaWindows.HottestCap);
        first.HottestRows.Should().HaveCount(KafkaWindows.HottestCap);
        first.Hottest.Should().Be(900);
        first.Rows.Should().OnlyContain(row => KafkaAttention.Partition(row.Leader, row.Replicas, row.InSync, row.Lag.Select(item => item.Lag)));
        first.CapNote.Should().Contain("8 of");

        var clamped = KafkaWindows.Topic(rows, 1, 7);
        clamped.PageSize.Should().Be(25);
        clamped.Rows.Should().HaveCount(25);

        var last = KafkaWindows.Topic(rows, 99, 25);
        last.Page.Should().Be(8);
        last.Rows.Should().HaveCount(25);
        KafkaWindows.Topic(rows, 0, 10).Page.Should().Be(1);

        KafkaWindows.LeaderSkew([-1, -1]).Should().Be("No leader is online.");
        KafkaWindows.LeaderSkew([2, 2, 2]).Should().Be("Broker 2 leads every partition.");
        KafkaWindows.LeaderSkew([0, 0, 0, 0, 0, 1]).Should().Be("Broker 0 leads 5 of 6 partitions.");
        KafkaWindows.LeaderSkew([0, 1, 2]).Should().Be("Leaders are spread across 3 brokers.");

        var committed = KafkaWindows.Committed(
            [
                new KafkaPartitionLagRow { Topic = "Quiet", Partition = 0, Lag = 0, Owned = true },
                new KafkaPartitionLagRow { Topic = "Hot", Partition = 1, Lag = 4, Owned = true },
                new KafkaPartitionLagRow { Topic = "Hot", Partition = 0, Lag = 9, Owned = false }
            ],
            1,
            10);
        committed.Rows[0].Lag.Should().Be(9);
        committed.Rows[1].Lag.Should().Be(4);
        committed.Hot.Should().Be(2);
        committed.Skew.Should().Be("Hot holds 13 of 13 lag.");

        var facts = KafkaWindows.Facts(
            [
                new KafkaPartitionFact { Partition = 0, Leader = 0, Replicas = [0, 1], Isr = [0, 1], PreferredLeader = true, LogStart = 0, HighWatermark = 10 },
                new KafkaPartitionFact { Partition = 1, Leader = -1, Replicas = [0, 1], Isr = [0], PreferredLeader = false, LogStart = 0, HighWatermark = 3 }
            ],
            1,
            10);
        facts.Rows[0].Partition.Should().Be(1);
        facts.Hot.Should().Be(1);

        KafkaWindows.AssignmentText(["a", "b", "c", "d", "e", "f", "g"]).Should().Be("7 partitions");
        KafkaWindows.UnownedText(["0", "1", "2", "3", "4", "5", "6", "7", "8"]).Should().EndWith("…");

        var root = RepoRoot();
        var topic = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Topic.cshtml"));
        var consumers = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Consumers.cshtml"));
        var migrate = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Migrate.cshtml"));
        var overview = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Overview.cshtml"));
        var messages = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Messages.cshtml"));
        topic.Should().Contain("KafkaWindows.Topic");
        topic.Should().NotContain("foreach (var row in Model.Partitions)");
        consumers.Should().Contain("KafkaWindows.Committed");
        consumers.Should().Contain("This group has no committed partitions.");
        consumers.Should().NotContain("foreach (var row in selected.Partitions");
        migrate.Should().Contain("KafkaWindows.Facts");
        migrate.Should().NotContain("foreach (var row in detail.PartitionsDetail)");
        overview.Should().Contain("KafkaWindows.Groups");
        overview.Should().NotContain("foreach (var row in Model.Groups.Groups");
        overview.Should().Contain("Full topic table");
        messages.Should().Contain("aria-label=\"Message pages\"");
        messages.Should().Contain(">First</");
        messages.Should().Contain(">Last</");
        messages.Should().NotContain(">Earlier</a>");
        messages.Should().NotContain(">Later</a>");
        messages.Should().NotContain("name=\"limit\"");
    }

    [Fact]
    public void Partition_page_stays_off_the_address_until_a_later_page()
    {
        var query = new ThroughputKafkaPageQuery
        {
            View = ThroughputKafkaPageQuery.Topic,
            TopicName = "ResourcesAcquired",
            PartPage = 3,
            OpenRecord = "0:120"
        };
        query.Href().Should().Contain("part=3");
        query.Href(part: 1).Should().NotContain("part=");
        query.Href(view: ThroughputKafkaPageQuery.Overview, page: 1).Should().NotContain("part=");
        query.Href(view: ThroughputKafkaPageQuery.Overview, page: 1).Should().NotContain("partition=");
        query.Href(topic: "Other").Should().NotContain("part=");
        query.Href(part: 2).Should().Contain("part=2");

        var parsed = ThroughputKafkaPageQuery.From(new QueryCollection(new Dictionary<string, StringValues>
        {
            ["view"] = "topic",
            ["part"] = "4"
        }), null);
        parsed.PartPage.Should().Be(4);

        var read = KafkaMessageWindow.Locate(40, 25, 0, 5000);
        read.Offset.Should().Be(975);
        read.Count.Should().Be(25);
        KafkaMessageWindow.Locate(1, 25, 0, 5000).Offset.Should().Be(0);
        KafkaMessageWindow.Pages(2, 25, 120).Should().Be(1);
        KafkaMessageWindow.Pages(0, 25, 120).Should().Be(5);
        KafkaMessageWindow.Matches(
            new KafkaBrowseRecord { Key = "cap", Value = "kept" },
            null, "cap", null, null, null, null, null).Should().BeTrue();
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
}
