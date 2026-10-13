using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Link.UI.Tests;

public class KafkaTopicFamilyTests
{
    private static KafkaTopicRow Row(string topic, int partitions = 3, int retry = 0, int error = 0) =>
        new() { Topic = topic, Family = topic, Partitions = partitions, RetryPartitions = retry, ErrorPartitions = error };

    [Fact]
    public void Error_and_retry_names_resolve_to_their_main_row_when_they_exist()
    {
        var rows = new[] { Row("ReadyToAcquire", retry: 3, error: 3), Row("PatientEvent") };

        KafkaTopicFamilyRules.Owner("ReadyToAcquire-Error", rows)!.Topic.Should().Be("ReadyToAcquire");
        KafkaTopicFamilyRules.Owner("ReadyToAcquire-Retry", rows)!.Topic.Should().Be("ReadyToAcquire");
        KafkaTopicFamilyRules.Owner("ReadyToAcquire", rows)!.Topic.Should().Be("ReadyToAcquire");
    }

    [Fact]
    public void A_sibling_that_is_not_on_the_cluster_or_a_redrive_name_does_not_resolve()
    {
        var rows = new[] { Row("PatientEvent") };

        KafkaTopicFamilyRules.Owner("PatientEvent-Error", rows).Should().BeNull();
        KafkaTopicFamilyRules.Owner("PatientEvent-Retry", rows).Should().BeNull();
        KafkaTopicFamilyRules.Owner("PatientEvent-Redrive-Worker", rows).Should().BeNull();
        KafkaTopicFamilyRules.Owner("-Error", rows).Should().BeNull();
    }

    [Fact]
    public void Members_follow_link_naming_and_list_only_siblings_that_exist()
    {
        var members = KafkaTopicFamilyRules.Members(Row("ReadyToAcquire", retry: 3, error: 2), null);

        members.Select(member => member.Topic).Should().Equal("ReadyToAcquire", "ReadyToAcquire-Error", "ReadyToAcquire-Retry");
        members.Should().OnlyContain(member => member.Exists && member.Main == "ReadyToAcquire");
        members.Single(member => member.Kind == KafkaBrowseAllowList.KindError).Partitions.Should().Be(2);
        members.Should().NotContain(member => member.Kind == KafkaBrowseAllowList.KindRedrive);
        KafkaTopicFamilyRules.CanRedrive(members).Should().BeFalse("empty error and retry topics hold nothing to redrive");
        KafkaTopicFamilyRules.Kinds(members).Should().Equal("", "Main", "Error", "Retry");
    }

    [Fact]
    public void A_topic_without_siblings_has_only_main_and_nothing_to_redrive()
    {
        var members = KafkaTopicFamilyRules.Members(Row("PatientEvent"), null);

        members.Select(member => member.Topic).Should().Equal("PatientEvent");
        KafkaTopicFamilyRules.CanRedrive(members).Should().BeFalse();
        KafkaTopicFamilyRules.Kinds(members).Should().Equal("", "Main");
        KafkaTopicFamilyRules.RedriveSource("PatientEvent", members).Should().BeNull();
    }

    [Fact]
    public void Existence_never_comes_from_the_family_read_but_offsets_do()
    {
        var family = new KafkaFamilyView
        {
            Main = "ReadyToAcquire",
            Members =
            [
                new KafkaNamedTopic { Topic = "ReadyToAcquire-Error", Kind = KafkaBrowseAllowList.KindError, Exists = true, HighWatermarkSum = 42, LagKnown = true, Lag = 5 },
                new KafkaNamedTopic { Topic = "ReadyToAcquire-Retry", Kind = KafkaBrowseAllowList.KindRetry, Exists = true },
                new KafkaNamedTopic { Topic = "ReadyToAcquire-Redrive-DataAcquisitionWorker", Kind = KafkaBrowseAllowList.KindRedrive, Exists = true }
            ]
        };

        var members = KafkaTopicFamilyRules.Members(Row("ReadyToAcquire", error: 3), family);

        members.Select(member => member.Topic).Should().Equal("ReadyToAcquire", "ReadyToAcquire-Error");
        members[1].HighWatermarkSum.Should().Be(42);
        members[1].Lag.Should().Be(5);
        KafkaTopicFamilyRules.CanRedrive(members).Should().BeTrue();
    }

    [Fact]
    public void Redrive_reads_from_the_error_or_retry_topic_only()
    {
        var members = KafkaTopicFamilyRules.Members(Row("ReadyToAcquire", retry: 3, error: 3), null);

        KafkaTopicFamilyRules.RedriveSource("ReadyToAcquire-Error", members)!.Kind.Should().Be(KafkaBrowseAllowList.KindError);
        KafkaTopicFamilyRules.RedriveSource("ReadyToAcquire-Retry", members)!.Kind.Should().Be(KafkaBrowseAllowList.KindRetry);
        KafkaTopicFamilyRules.RedriveSource("ReadyToAcquire", members).Should().BeNull();
        KafkaTopicFamilyRules.RedriveSource("PatientEvent-Error", members).Should().BeNull();
    }

    [Fact]
    public void The_redrive_source_rides_with_a_staged_record_and_drops_without_one()
    {
        var query = ThroughputKafkaPageQuery.From(new QueryCollection(new Dictionary<string, StringValues>
        {
            ["view"] = "messages",
            ["topic"] = "ReadyToAcquire-Error",
            ["record"] = "0:7"
        }), "/Operations");

        var href = query.Href(view: ThroughputKafkaPageQuery.Produce, topic: "ReadyToAcquire", stage: "0:7", redrive: "ReadyToAcquire-Error");
        href.Should().StartWith("/Operations/Kafka/Topics/ReadyToAcquire/Produce?");
        href.Should().Contain("stage=0%3A7").And.Contain("redrive=ReadyToAcquire-Error");

        var parsed = ThroughputKafkaPageQuery.From(new QueryCollection(new Dictionary<string, StringValues>
        {
            ["view"] = "produce",
            ["topic"] = "ReadyToAcquire",
            ["stage"] = "0:7",
            ["redrive"] = "ReadyToAcquire-Error"
        }), "/Operations");
        parsed.RedriveFrom.Should().Be("ReadyToAcquire-Error");
        query.Href(view: ThroughputKafkaPageQuery.Produce, topic: "ReadyToAcquire", redrive: "ReadyToAcquire-Error").Should().NotContain("redrive=");
    }

    [Fact]
    public void The_console_no_longer_links_guessed_redrive_or_service_topics()
    {
        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Operations/_Messages.cshtml"));
        view.Should().NotContain("\"Redrive\" })");
        view.Should().NotContain("KafkaBrowseAllowList.MembersOf(topicName)");
        view.Should().Contain("KafkaTopicFamilyRules.Kinds(members)");

        var controller = File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/OperationsController.cs"));
        controller.Should().Contain("KafkaTopicFamilyRules.Owner(query.TopicName, topics.Topics)");
        controller.Should().Contain("KafkaTopicFamilyRules.Members(selectedTopic, familyCall.Value)");
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "link-cloud.sln")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
