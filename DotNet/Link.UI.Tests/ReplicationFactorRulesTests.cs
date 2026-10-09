using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Link.UI.Tests;

public class ReplicationFactorRulesTests
{
    [Fact]
    public void Decrease_keeps_the_leader_and_spreads_racks()
    {
        var evaluation = ReplicationFactorRules.Evaluate(
            "ResourcesAcquired",
            2,
            ReplicationFactorRules.DefaultThrottle,
            2,
            true,
            Brokers(),
            SamplePlacements(),
            "",
            1,
            25);

        evaluation.Plan.Accepted.Should().BeTrue(evaluation.Plan.Summary);
        evaluation.Plan.Summary.Should().Contain("3 to 2");
        evaluation.Plan.Summary.Should().Contain("10 MiB/s");
        evaluation.Assignments.Should().HaveCount(3);
        foreach (var row in evaluation.Assignments)
        {
            row.After.Should().HaveCount(2);
            row.After.Should().Contain(row.Leader);
            row.Removed.Should().HaveCount(1);
            row.Removed.Should().NotContain(row.Leader);
            row.RackShared.Should().BeFalse();
        }

        evaluation.Plan.Rows.Should().HaveCount(3);
        evaluation.Plan.Hottest.Should().OnlyContain(broker => broker.After <= Math.Max(evaluation.Plan.FairShare, broker.Leaders));
        evaluation.Plan.Skew.Should().Be("Each broker would hold 2 replicas.");
    }

    [Fact]
    public void Target_above_the_online_broker_count_is_refused()
    {
        var evaluation = ReplicationFactorRules.Evaluate("ResourcesAcquired", 4, ReplicationFactorRules.DefaultThrottle, 2, true, Brokers(), SamplePlacements(), "", 1, 25);
        evaluation.Plan.Accepted.Should().BeFalse();
        evaluation.Plan.Summary.Should().Contain("online");
        evaluation.Plan.CurrentFactor.Should().Be(3);
        evaluation.Plan.TargetFactor.Should().Be(4);
        evaluation.Plan.Skew.Should().Be("Not planned.");
        evaluation.Plan.ChangedCount.Should().Be(0);
        evaluation.Assignments.Should().BeEmpty();
        evaluation.Plan.Rows.Should().BeEmpty();
    }

    [Fact]
    public void Target_below_min_insync_replicas_is_refused()
    {
        var evaluation = ReplicationFactorRules.Evaluate("ResourcesAcquired", 1, ReplicationFactorRules.DefaultThrottle, 2, true, Brokers(), SamplePlacements(), "", 1, 25);
        evaluation.Plan.Accepted.Should().BeFalse();
        evaluation.Plan.Summary.Should().Contain("min.insync.replicas");
        evaluation.Assignments.Should().BeEmpty();
    }

    [Fact]
    public void An_offline_replica_refuses_the_whole_plan()
    {
        var brokers = Brokers();
        brokers[2].State = "offline";
        var evaluation = ReplicationFactorRules.Evaluate("ResourcesAcquired", 2, ReplicationFactorRules.DefaultThrottle, 2, true, brokers, SamplePlacements(), "", 1, 25);
        evaluation.Plan.Accepted.Should().BeFalse();
        evaluation.Plan.Errors.Should().Contain(error => error.Contains("offline", StringComparison.Ordinal));
        evaluation.Assignments.Should().BeEmpty();
    }

    [Fact]
    public void A_missing_throttle_is_refused()
    {
        var evaluation = ReplicationFactorRules.Evaluate("ResourcesAcquired", 2, 0, 2, true, Brokers(), SamplePlacements(), "", 1, 25);
        evaluation.Plan.Accepted.Should().BeFalse();
        evaluation.Plan.Summary.Should().Contain("throttle");
    }

    [Fact]
    public void Hundreds_of_partitions_are_paged_worst_first()
    {
        var placements = Enumerable.Range(0, 200).Select(id => new PartitionPlacement
        {
            Topic = "ResourcesAcquired",
            Partition = id,
            Leader = id % 3,
            Replicas = [id % 3, (id + 1) % 3, (id + 2) % 3],
            Isr = [id % 3, (id + 1) % 3, (id + 2) % 3]
        }).ToList();

        var first = ReplicationFactorRules.Evaluate("ResourcesAcquired", 2, ReplicationFactorRules.DefaultThrottle, 2, true, Brokers(), placements, "", 1, 25);
        first.Plan.Accepted.Should().BeTrue(first.Plan.Summary);
        first.Plan.PartitionCount.Should().Be(200);
        first.Plan.MatchCount.Should().Be(200);
        first.Plan.Pages.Should().Be(8);
        first.Plan.Rows.Should().HaveCount(25);
        first.Plan.Rows[0].Partition.Should().Be(0);
        first.Assignments.Should().HaveCount(200);

        var second = ReplicationFactorRules.Evaluate("ResourcesAcquired", 2, ReplicationFactorRules.DefaultThrottle, 2, true, Brokers(), placements, "", 2, 25);
        second.Plan.Rows[0].Partition.Should().Be(25);

        var found = ReplicationFactorRules.Evaluate("ResourcesAcquired", 2, ReplicationFactorRules.DefaultThrottle, 2, true, Brokers(), placements, "50", 1, 25);
        found.Plan.MatchCount.Should().Be(2);
        found.Plan.Rows.Select(row => row.Partition).Should().BeEquivalentTo([50, 150]);
    }

    [Fact]
    public void Hottest_brokers_are_capped_and_a_leader_may_stay_above_a_fair_share()
    {
        var many = Enumerable.Range(0, 20).Select(id => new BrokerSnapshot { Id = id, Rack = "r" + id, State = "up" }).ToList();
        var spread = Enumerable.Range(0, 20).Select(id => new PartitionPlacement
        {
            Topic = "Wide",
            Partition = id,
            Leader = id,
            Replicas = [id],
            Isr = [id]
        }).ToList();
        var wide = ReplicationFactorRules.Evaluate("Wide", 2, ReplicationFactorRules.DefaultThrottle, 1, true, many, spread, "", 1, 25);
        wide.Plan.Accepted.Should().BeTrue(wide.Plan.Summary);
        wide.Plan.Hottest.Should().HaveCount(ReplicationFactorRules.HottestCap);
        wide.Plan.BrokerCount.Should().Be(20);

        var sticky = Enumerable.Range(0, 9).Select(id => new PartitionPlacement
        {
            Topic = "Sticky",
            Partition = id,
            Leader = 0,
            Replicas = [0],
            Isr = [0]
        }).ToList();
        var kept = ReplicationFactorRules.Evaluate("Sticky", 2, ReplicationFactorRules.DefaultThrottle, 1, true, Brokers(), sticky, "", 1, 25);
        kept.Plan.Accepted.Should().BeTrue(kept.Plan.Summary);
        kept.Plan.Hottest.Single(broker => broker.Id == 0).After.Should().Be(9);
        kept.Plan.Skew.Should().Contain("Broker 0 would hold 9 replicas.");
        kept.Assignments.Should().OnlyContain(row => row.After.Contains(0) && row.After.Count == 2);
    }

    [Fact]
    public void Fixture_request_throttles_reassigns_and_clears_the_throttle()
    {
        var fixture = Fixture();
        var before = fixture.Cluster().Value!.Placements
            .Where(row => row.Topic == "ResourcesAcquired")
            .Select(row => row.Replicas.Count)
            .ToList();
        before.Should().Equal(3, 3, 3);

        var refused = fixture.CreateReplicationFactor("ResourcesAcquired", 4, ReplicationFactorRules.DefaultThrottle, "too high", "ResourcesAcquired", "corr");
        refused.Value.Should().BeNull();
        refused.Error.Should().Contain("online");
        fixture.Cluster().Value!.Placements.Where(row => row.Topic == "ResourcesAcquired").Should().OnlyContain(row => row.Replicas.Count == 3);

        var below = fixture.PlanReplicationFactor("ResourcesAcquired", 1, ReplicationFactorRules.DefaultThrottle, "", 1, 25);
        below.Value!.Accepted.Should().BeFalse();
        below.Error.Should().Contain("min.insync.replicas");

        var created = fixture.CreateReplicationFactor("ResourcesAcquired", 2, ReplicationFactorRules.DefaultThrottle, "drop a replica", "ResourcesAcquired", "corr-rf");
        created.Value.Should().NotBeNull();
        var record = created.Value!;
        record.Status.Should().Be("Done");
        record.Kind.Should().Be("ReplicationFactor");
        record.ThrottleCleared.Should().BeTrue();
        record.Progress.Should().Contain("ISR");
        record.Steps.Should().HaveCount(4);
        record.Steps[0].Should().Contain("Throttle set");
        record.Steps[3].Should().Be("Throttle cleared.");
        record.BeforeReplicationFactor.Should().Be(3);
        record.TargetReplicationFactor.Should().Be(2);

        var moved = fixture.Cluster().Value!.Placements.Where(row => row.Topic == "ResourcesAcquired").ToList();
        moved.Should().OnlyContain(row => row.Replicas.Count == 2 && row.Isr.SequenceEqual(row.Replicas));
        fixture.Topics().Value!.Topics.Single(row => row.Topic == "ResourcesAcquired").ReplicationFactor.Should().Be(2);
        fixture.Topics().Value!.Topics.Single(row => row.Topic == "DataAcquisitionRequested").ReplicationFactor.Should().Be(3);
        fixture.Cluster().Value!.Placements.Where(row => row.Topic == "DataAcquisitionRequested").Should().OnlyContain(row => row.Replicas.Count == 3);
    }

    [Fact]
    public void Replication_query_is_kept_on_the_topic_and_dropped_when_the_topic_changes()
    {
        var query = new ThroughputKafkaPageQuery
        {
            View = ThroughputKafkaPageQuery.Topic,
            TopicName = "ResourcesAcquired",
            Rf = 2,
            RfPage = 2,
            RfSize = 10,
            RfQ = "7",
            RfThrottle = ReplicationFactorRules.DefaultThrottle
        };
        var href = query.Href();
        href.Should().Contain("rf=2");
        href.Should().Contain("rfPage=2");
        href.Should().Contain("rfSize=10");
        href.Should().Contain("rfQ=7");
        href.Should().Contain("rfThrottle=" + ReplicationFactorRules.DefaultThrottle);
        query.Href(view: ThroughputKafkaPageQuery.Overview, page: 1).Should().NotContain("rf=");
        query.Href(topic: "Other").Should().NotContain("rf=");
        query.WithReplication(page: 3).Href().Should().Contain("rfPage=3");

        var parsed = ThroughputKafkaPageQuery.From(new QueryCollection(new Dictionary<string, StringValues>
        {
            ["view"] = "topic",
            ["topic"] = "ResourcesAcquired",
            ["rf"] = "2",
            ["rfPage"] = "3",
            ["rfSize"] = "10",
            ["rfQ"] = "12",
            ["rfThrottle"] = "10485760"
        }), null);
        parsed.Rf.Should().Be(2);
        parsed.RfPage.Should().Be(3);
        parsed.RfSize.Should().Be(10);
        parsed.RfQ.Should().Be("12");
        parsed.RfThrottle.Should().Be(10485760);
    }

    [Fact]
    public void Topic_page_offers_a_guarded_replication_factor_preview()
    {
        var topic = File.ReadAllText(Path.Combine(RepoRoot(), "Views", "Operations", "_Topic.cshtml"));
        var form = File.ReadAllText(Path.Combine(RepoRoot(), "Views", "Operations", "_ReplicationFactor.cshtml"));
        var request = File.ReadAllText(Path.Combine(RepoRoot(), "Views", "Operations", "_ChangeRequest.cshtml"));
        topic.Should().Contain("partial name=\"_ReplicationFactor\"");
        form.Should().Contain("Change replication factor");
        form.Should().Contain("id=\"kafka-replication-result\"");
        form.Should().Contain("data-lu-result=\"kafka-replication\"");
        form.Should().Contain(">Preview replication factor</span>");
        form.Should().Contain("btn-au-execute");
        form.Should().Contain(">Request</span>");
        form.Should().Contain("btn-success");
        form.Should().NotContain("btn-outline-");
        request.Should().Contain("ReplicationFactor");
        request.Should().Contain("id=\"kafka-replication-steps\"");
        IndexOf(form, "id=\"kafka-replication-result\"").Should().BeLessThan(IndexOf(form, ">Preview replication factor</span>"));
    }

    private static List<BrokerSnapshot> Brokers() =>
    [
        new BrokerSnapshot { Id = 0, Rack = "a", State = "up", PartitionCount = 4 },
        new BrokerSnapshot { Id = 1, Rack = "b", State = "up", PartitionCount = 4 },
        new BrokerSnapshot { Id = 2, Rack = "c", State = "up", PartitionCount = 4 }
    ];

    private static List<PartitionPlacement> SamplePlacements() =>
    [
        new PartitionPlacement { Topic = "ResourcesAcquired", Partition = 0, Leader = 0, Replicas = [0, 1, 2], Isr = [0, 1, 2] },
        new PartitionPlacement { Topic = "ResourcesAcquired", Partition = 1, Leader = 1, Replicas = [1, 2, 0], Isr = [1, 2, 0] },
        new PartitionPlacement { Topic = "ResourcesAcquired", Partition = 2, Leader = 2, Replicas = [2, 0, 1], Isr = [2, 0, 1] }
    ];

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
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
