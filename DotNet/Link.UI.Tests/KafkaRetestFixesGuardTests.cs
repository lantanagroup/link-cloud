using FluentAssertions;
using Link.UI.Models;
using Xunit;

namespace Link.UI.Tests;

public class KafkaRetestFixesGuardTests
{
    private static string Ops(string file)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "Link.UI", "Views", "Operations")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(dir!, "Link.UI", "Views", "Operations", file));
    }

    [Fact]
    public void Export_href_names_the_topic_for_path_style_routes()
    {
        var query = new ThroughputKafkaPageQuery { View = ThroughputKafkaPageQuery.Messages, TopicName = "ResourcesAcquired" };
        query.ExportHref().Should().StartWith("/Operations/Kafka/messages/export?");
        query.ExportHref().Should().Contain("topic=ResourcesAcquired");
    }

    [Fact]
    public void Produce_result_lives_in_the_produce_card_and_requests_hide_actions_when_done()
    {
        Ops("_ProduceMessage.cshtml").Should().Contain("id=\"kafka-produce-result\"");
        Ops("Kafka.cshtml").Should().Contain("Model.Request is { Kind: \"Produce\" } && topicTab == KafkaConsoleRoutes.Produce");
        var change = Ops("_ChangeRequest.cshtml");
        change.Should().Contain("@if (!terminal)");
        change.Should().NotContain("Requester @request.Requester");
        change.Should().Contain("<summary>Details</summary>");
    }

    [Fact]
    public void Unknown_topic_group_and_broker_show_not_found()
    {
        Ops("Kafka.cshtml").Should().Contain("new KafkaNotFound(\"Topic\"");
        Ops("_Consumers.cshtml").Should().Contain("new KafkaNotFound(\"Group\"");
        Ops("_Brokers.cshtml").Should().Contain("new KafkaNotFound(\"Broker\"");
    }

    [Fact]
    public void Row_actions_are_icon_only_and_labels_do_not_repeat()
    {
        var brokers = Ops("_Brokers.cshtml");
        brokers.Should().Contain("title=\"Open topic @row.Topic\"");
        brokers.Should().NotContain("btn btn-sm btn-au-link\" href=\"@KafkaConsoleRoutes.Topics(row.Topic");
        Ops("_Consumers.cshtml").Should().NotContain("LabeledId { Label = \"Group\"");
        var messages = Ops("_Messages.cshtml");
        messages.Should().Contain("bi-chevron-down");
        messages.Should().NotContain("btn-warning\" href=\"@query.Href(closeRecord: true)\"");
        Ops("_MessageDetail.cshtml").Should().Contain("<td class=\"lu-shrink\">@header.Name</td>");
    }

    [Fact]
    public void Partition_actions_sit_beside_the_inputs()
    {
        var migrate = Ops("_Migrate.cshtml");
        migrate.IndexOf("id=\"choice-add\"", StringComparison.Ordinal).Should().BeLessThan(migrate.IndexOf("id=\"plan-partitions\"", StringComparison.Ordinal));
        migrate.IndexOf("id=\"plan-confirm\"", StringComparison.Ordinal).Should().BeLessThan(migrate.IndexOf("title=\"Preview increase\"", StringComparison.Ordinal));
    }
}
