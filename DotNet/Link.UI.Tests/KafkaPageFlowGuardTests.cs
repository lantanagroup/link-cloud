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
            "Index.cshtml",
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
        topic.Should().Contain("ThroughputKafkaPageQuery.Migrate");
        topic.Should().Contain("topic: topic.Topic");
        topic.Should().Contain("btn btn-au-link");
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
        IndexOf(slice, "id=\"kafka-group-table\"").Should().BeLessThan(IndexOf(slice, ">Preview add</span>"));
        slice.Should().Contain("Needs attention");
    }

    [Fact]
    public void Migrate_still_shows_eligibility_and_the_dry_run()
    {
        var migrate = File.ReadAllText(Path.Combine(RepoRoot(), "Views", "Operations", "_Migrate.cshtml"));
        migrate.Should().Contain("KafkaIncreaseEligibility.Line");
        migrate.Should().Contain("KafkaIncreaseEligibility.MigrationLine");
        migrate.Should().Contain("id=\"kafka-partition-result\"");
        migrate.Should().Contain("id=\"kafka-dry-run-result\"");
        migrate.Should().Contain(">Dry run</button>");
        migrate.Should().Contain(">Preview increase</span>");
        migrate.Should().NotContain("Slice1Eligible");
    }

    [Fact]
    public void Search_sits_on_the_table_it_filters()
    {
        var root = RepoRoot();
        var overview = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Overview.cshtml"));
        var overviewStart = IndexOf(overview, "id=\"kafka-topics\"");
        var overviewEnd = overview.IndexOf("</section>", overviewStart, StringComparison.Ordinal);
        var topics = overview[overviewStart..overviewEnd];
        IndexOf(topics, "name=\"q\"").Should().BeLessThan(IndexOf(topics, "id=\"kafka-topic-table\""));

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
