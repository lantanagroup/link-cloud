using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class KafkaPageFlowGuardTests
{
    [Fact]
    public void Kafka_views_have_no_step_lists()
    {
        var root = RepoRoot();
        foreach (var name in new[]
        {
            "Kafka.cshtml",
            "_Overview.cshtml",
            "_Topic.cshtml",
            "_Consumers.cshtml",
            "_Brokers.cshtml",
            "_Messages.cshtml",
            "_Migrate.cshtml",
            "_ReplicationFactor.cshtml"
        })
        {
            var text = File.ReadAllText(Path.Combine(root, "Views", "Operations", name));
            text.Should().NotContain("lu-kafka-steps", name);
            text.Should().NotContain("ForWizardStep", name);
        }

        File.ReadAllText(Path.Combine(root, "wwwroot", "css", "site.css")).Should().NotContain(".lu-kafka-steps");
    }

    [Fact]
    public void Topic_links_to_migrate_and_does_not_repeat_the_partition_form()
    {
        var topic = File.ReadAllText(Path.Combine(RepoRoot(), "Views", "Operations", "_Topic.cshtml"));
        topic.Should().NotContain("name=\"partitions\"");
        topic.Should().NotContain("Preview increase");
        topic.Should().NotContain("Preview family");
        topic.Should().NotContain("Migrate instead");
        topic.Should().NotContain("Not checked yet");
        topic.Should().NotContain("Dry run a migration");
        topic.Should().NotContain("KafkaIncreaseEligibility");
        topic.Should().NotContain("ThroughputKafkaPageQuery.Migrate", "partition changes are reached from the Partitions sub-tab");
        topic.Should().Contain("btn-au-link");
        File.ReadAllText(Path.Combine(RepoRoot(), "Views", "Operations", "_TopicTabs.cshtml")).Should().Contain("view: ThroughputKafkaPageQuery.Migrate, topic: topic)\">Partitions</a>");
        File.ReadAllText(Path.Combine(RepoRoot(), "Views", "Operations", "_TopicPicker.cshtml")).Should().Contain("id=\"topic-pick\"");
    }

    [Fact]
    public void Consumers_has_one_groups_table_with_search_and_the_actions()
    {
        var consumers = File.ReadAllText(Path.Combine(RepoRoot(), "Views", "Operations", "_Consumers.cshtml"));
        consumers.Should().NotContain("Groups that need attention");
        consumers.Should().NotContain("Full group table");
        Count(consumers, "id=\"kafka-group-table\"").Should().Be(1);
        var start = IndexOf(consumers, "id=\"kafka-groups\"");
        var end = consumers.IndexOf("</section>", start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start);
        var slice = consumers[start..end];
        IndexOf(slice, "name=\"q\"").Should().BeLessThan(IndexOf(slice, "id=\"kafka-group-table\""));
        slice.Should().NotContain(">Preview");
        slice.Should().Contain("Needs attention");
        consumers.Should().Contain("id=\"kafka-group-card\"");
        IndexOf(consumers, "name=\"replicas\"").Should().BeLessThan(IndexOf(consumers, "Committed partitions"));
        IndexOf(consumers, ">Preview</span>").Should().BeLessThan(IndexOf(consumers, "Committed partitions"));
    }

    [Fact]
    public void Migrate_still_shows_eligibility_and_the_dry_run()
    {
        var migrate = File.ReadAllText(Path.Combine(RepoRoot(), "Views", "Operations", "_Migrate.cshtml"));
        migrate.Should().Contain("KafkaIncreaseEligibility.Line");
        migrate.Should().Contain("KafkaIncreaseEligibility.MigrationLine");
        migrate.Should().Contain("id=\"kafka-partition-result\"");
        migrate.Should().Contain("id=\"kafka-dry-run-result\"");
        migrate.Should().Contain("formaction=\"/Operations/Kafka/migrations/plan\"");
        migrate.Should().Contain("formaction=\"/Operations/Kafka/plan\"");
        migrate.Should().Contain("aria-label=\"Preview increase\"");
        migrate.Should().NotContain("Slice1Eligible");
        Count(migrate, "id=\"kafka-change-partitions\"").Should().Be(1);
        migrate.Should().NotContain("KafkaWindows.Facts");
        migrate.Should().NotContain("<h2 class=\"card-header h6 mb-0\">Add partitions");
        migrate.Should().NotContain("<h2 class=\"card-header h6 mb-0\">Migrate");
    }

    [Fact]
    public void Overview_lists_what_needs_attention_and_cards_put_actions_first()
    {
        var root = RepoRoot();
        var overview = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Overview.cshtml"));
        overview.Should().Contain("id=\"kafka-attention\"");
        overview.Should().Contain("Needs attention");
        overview.Should().Contain("KafkaNeedsAttention.Healthy");
        overview.Should().Contain("lu-kafka-attention");
        overview.Should().NotContain("id=\"kafka-topic-table\"");
        overview.Should().NotContain("id=\"kafka-overview-groups\"");
        overview.Should().NotContain(">Migrate</a>");
        overview.Should().NotContain("form-text");
        overview.Should().NotContain("Provider @");
        overview.Should().NotContain("kafkaRateChart");

        var consumers = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Consumers.cshtml"));
        IndexOf(consumers, ">Preview</span>").Should().BeLessThan(IndexOf(consumers, "Committed partitions"));
        var brokers = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Brokers.cshtml"));
        IndexOf(brokers, ">Preview decommission</span>").Should().BeLessThan(IndexOf(brokers, "Partitions on this broker"));

        foreach (var name in new[]
        {
            "Kafka.cshtml", "_Overview.cshtml", "_Topic.cshtml", "_Consumers.cshtml", "_Brokers.cshtml",
            "_Messages.cshtml", "_Migrate.cshtml", "_ReplicationFactor.cshtml", "_ProduceMessage.cshtml",
            "_TopicPicker.cshtml", "_TopicTabs.cshtml", "_TopicConsumers.cshtml"
        })
        {
            File.ReadAllText(Path.Combine(root, "Views", "Operations", name)).Should().NotContain("form-text", name);
        }
    }

    [Fact]
    public void Search_sits_on_the_table_it_filters()
    {
        var root = RepoRoot();
        var overview = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Overview.cshtml"));
        overview.Should().Contain("id=\"kafka-attention\"");
        overview.Should().NotContain("id=\"kafka-topic-table\"");
        overview.Should().NotContain("name=\"q\"");

        var messages = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Messages.cshtml"));
        var messageStart = IndexOf(messages, "id=\"kafka-records\"");
        var messageEnd = messages.LastIndexOf("</section>", StringComparison.Ordinal);
        var records = messages[messageStart..messageEnd];
        IndexOf(records, "id=\"browse-q\"").Should().BeLessThan(IndexOf(records, "<table"));
    }

    private static int Count(string text, string needle) =>
        text.Split(needle).Length - 1;

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
