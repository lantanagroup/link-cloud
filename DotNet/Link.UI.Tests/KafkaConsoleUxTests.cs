using FluentAssertions;
using Link.UI.Services;
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
