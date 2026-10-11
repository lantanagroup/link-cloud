using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Link.UI.Tests;

public class KafkaConsoleRouteTests
{
    [Fact]
    public void Paths_place_the_topic_group_and_broker_in_the_path()
    {
        KafkaConsoleRoutes.Overview().Should().Be("/Operations/Kafka");
        KafkaConsoleRoutes.Topics("ResourcesAcquired", null).Should().Be("/Operations/Kafka/Topics/ResourcesAcquired/Summary");
        KafkaConsoleRoutes.Topics("ResourcesAcquired", "messages").Should().Be("/Operations/Kafka/Topics/ResourcesAcquired/Messages");
        KafkaConsoleRoutes.Topics("", "Messages").Should().Be("/Operations/Kafka/Topics");
        KafkaConsoleRoutes.Group("Normalization").Should().Be("/Operations/Kafka/ConsumerGroups/Normalization");
        KafkaConsoleRoutes.Group("").Should().Be("/Operations/Kafka/ConsumerGroups");
        KafkaConsoleRoutes.Broker("0").Should().Be("/Operations/Kafka/Brokers/0");
        KafkaConsoleRoutes.CanonicalSubtab("partitions").Should().Be(KafkaConsoleRoutes.Partitions);
        KafkaConsoleRoutes.CanonicalSubtab("schema").Should().BeNull();
        KafkaConsoleRoutes.Subtabs.Should().Equal(
            "Summary", "Messages", "Produce", "Partitions", "Replication", "Consumers");
    }

    [Fact]
    public void Old_view_urls_redirect_to_the_new_paths_and_keep_the_selection()
    {
        KafkaConsoleRoutes.LegacyRedirect(Query(new Dictionary<string, StringValues>
        {
            ["view"] = "overview",
            ["q"] = "acq"
        })).Should().Be("/Operations/Kafka?q=acq");

        KafkaConsoleRoutes.LegacyRedirect(Query(new Dictionary<string, StringValues>
        {
            ["view"] = "topic",
            ["topic"] = "ResourcesAcquired",
            ["page"] = "2"
        })).Should().Be("/Operations/Kafka/Topics/ResourcesAcquired/Summary?page=2");

        KafkaConsoleRoutes.LegacyRedirect(Query(new Dictionary<string, StringValues>
        {
            ["view"] = "messages",
            ["topic"] = "ResourcesAcquired",
            ["record"] = "0:1042"
        })).Should().Be("/Operations/Kafka/Topics/ResourcesAcquired/Messages?record=0%3A1042");

        KafkaConsoleRoutes.LegacyRedirect(Query(new Dictionary<string, StringValues>
        {
            ["view"] = "migrate",
            ["topic"] = "ResourcesAcquired",
            ["migration"] = "22222222-2222-2222-2222-222222222222"
        })).Should().Be("/Operations/Kafka/Topics/ResourcesAcquired/Partitions?migration=22222222-2222-2222-2222-222222222222");

        KafkaConsoleRoutes.LegacyRedirect(Query(new Dictionary<string, StringValues>
        {
            ["view"] = "consumers",
            ["group"] = "Normalization",
            ["tests"] = "1"
        })).Should().Be("/Operations/Kafka/ConsumerGroups/Normalization?tests=1");

        KafkaConsoleRoutes.LegacyRedirect(Query(new Dictionary<string, StringValues>
        {
            ["view"] = "brokers",
            ["broker"] = "0"
        })).Should().Be("/Operations/Kafka/Brokers/0");

        KafkaConsoleRoutes.LegacyRedirect(Query(new Dictionary<string, StringValues>
        {
            ["q"] = "only"
        })).Should().BeNull();
    }

    [Fact]
    public void Topic_picker_is_singular_and_sub_tabs_keep_the_topic()
    {
        var root = RepoRoot();
        var views = Directory.EnumerateFiles(Path.Combine(root, "Views", "Operations"), "*.cshtml")
            .Select(File.ReadAllText)
            .ToList();
        views.Sum(text => Count(text, "id=\"topic-pick\"")).Should().Be(1);
        views.Sum(text => Count(text, "id=\"browse-topic\"")).Should().Be(0);
        views.Sum(text => Count(text, "id=\"migrate-topic\"")).Should().Be(0);
        views.Sum(text => Count(text, "for=\"kafka-family-topic\"")).Should().Be(0);

        var tabs = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_TopicTabs.cshtml"));
        foreach (var name in new[] { "Summary", "Messages", "Produce", "Partitions", "Replication", "Consumers" })
            tabs.Should().Contain(">" + name + "</a>");
        tabs.Should().Contain("topic: topic");
        tabs.Should().NotContain("Schema");

        var picker = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_TopicPicker.cshtml"));
        picker.Should().Contain("Copy topic");
        picker.Should().Contain("/Operations/Kafka/Topics/search");
        picker.Should().Contain("/Operations/Kafka/Topics/open");

        var controller = File.ReadAllText(Path.Combine(root, "Controllers", "OperationsController.cs"));
        var search = controller.IndexOf("Kafka/Topics/search", StringComparison.Ordinal);
        var open = controller.IndexOf("Kafka/Topics/open", StringComparison.Ordinal);
        var topic = controller.IndexOf("Kafka/Topics/{topic}", StringComparison.Ordinal);
        search.Should().BeGreaterThan(0);
        open.Should().BeGreaterThan(search);
        topic.Should().BeGreaterThan(open);

        var script = File.ReadAllText(Path.Combine(root, "wwwroot", "js", "kafka-ops.js"));
        script.Should().Contain("data-topic-search");
        script.Should().Contain("function searchTopics");
    }

    [Fact]
    public void Kafka_page_has_one_tab_strip_and_the_new_routes()
    {
        var root = RepoRoot();
        var page = File.ReadAllText(Path.Combine(root, "Views", "Operations", "Kafka.cshtml"));
        page.Should().NotContain("partial name=\"_Nav\"");
        page.Should().Contain("Consumer groups");
        page.Should().Contain("Topics");
        page.Should().NotContain(">Migrate</a>");
        page.Should().NotContain(">Messages</a>");
        Count(page, "lu-section-nav").Should().Be(1);

        var controller = File.ReadAllText(Path.Combine(root, "Controllers", "OperationsController.cs"));
        controller.Should().Contain("[HttpGet(\"Kafka/Topics/{topic}/{subtab}\")]");
        controller.Should().Contain("[HttpGet(\"Kafka/ConsumerGroups/{group}\")]");
        controller.Should().Contain("[HttpGet(\"Kafka/Brokers/{id}\")]");
        controller.Should().Contain("KafkaConsoleRoutes.LegacyRedirect");
        controller.Should().Contain("Redirect(target)");
    }

    private static int Count(string text, string needle) => text.Split(needle).Length - 1;

    private static QueryCollection Query(Dictionary<string, StringValues> values) => new(values);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }
}
