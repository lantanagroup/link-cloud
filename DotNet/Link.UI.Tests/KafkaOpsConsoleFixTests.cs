using System.Text.Json;
using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class KafkaOpsConsoleFixTests
{
    [Fact]
    public void Controller_tile_says_not_reported_when_the_quorum_leader_is_hidden()
    {
        var hidden = KafkaControllerTile.Display(null, "The KRaft quorum leader is a separate node and is not exposed to this client.");
        hidden.Value.Should().Be(KafkaControllerTile.NotReported);
        hidden.Caption.Should().Be(KafkaControllerTile.SeparateNodeCaption);
        hidden.Title.Should().Be("The KRaft quorum leader is a separate node and is not exposed to this client.");

        var known = KafkaControllerTile.Display(100, "");
        known.Value.Should().Be("100");
        known.Caption.Should().Be(KafkaControllerTile.KnownCaption);
        known.Title.Should().BeNull();

        var unread = KafkaControllerTile.Display(null, " ");
        unread.Value.Should().Be("—");
        unread.Caption.Should().Be(KafkaControllerTile.KnownCaption);
    }

    [Fact]
    public void Page_banner_does_not_repeat_the_dry_run_summary()
    {
        KafkaPlanBanner.BesidePlan(
            "This topic is order-sensitive. Every subscribed group needs zero lag.",
            "This topic is order-sensitive. Every subscribed group needs zero lag.",
            true).Should().BeNull();
        KafkaPlanBanner.BesidePlan("The operations service returned 400.", "Partitions can only increase.", true)
            .Should().Be("The operations service returned 400.");
        KafkaPlanBanner.BesidePlan("A reason is required.", null, false).Should().Be("A reason is required.");
        KafkaPlanBanner.BesidePlan("  ", "Refused.", true).Should().BeNull();
    }

    [Fact]
    public void Acknowledged_groups_split_on_commas_and_new_lines_and_keep_order_stable()
    {
        MigrationGroupList.Parse(" zeta, alpha\nzeta; beta ").Should().Equal("alpha", "beta", "zeta");
        MigrationGroupList.Parse("group one").Should().Equal("group one");
        MigrationGroupList.Canonical("b, a").Should().Be(MigrationGroupList.Canonical("a\nb"));
        MigrationGroupList.Parse(null).Should().BeEmpty();
    }

    [Fact]
    public void Request_refuses_when_the_dry_run_inputs_changed()
    {
        MigrationRequestGuard.Refusal(8, false, false, "alpha", "hash", true, 8, false, false, "alpha").Should().BeNull();
        MigrationRequestGuard.Refusal(8, false, false, "b, a", "hash", true, 8, false, false, "a\nb").Should().BeNull();
        MigrationRequestGuard.Refusal(9, false, false, "alpha", "hash", true, 8, false, false, "alpha")
            .Should().Be(MigrationRequestGuard.InputsDiffer);
        MigrationRequestGuard.Refusal(8, true, false, "alpha", "hash", true, 8, false, false, "alpha")
            .Should().Be(MigrationRequestGuard.InputsDiffer);
        MigrationRequestGuard.Refusal(8, false, false, "beta", "hash", true, 8, false, false, "alpha")
            .Should().Be(MigrationRequestGuard.InputsDiffer);
        MigrationRequestGuard.Refusal(8, false, false, "alpha", "  ", true, 8, false, false, "alpha")
            .Should().Be(MigrationRequestGuard.NeedsDryRun);
        MigrationRequestGuard.Refusal(8, false, false, "alpha", "hash", false, 8, false, false, "alpha")
            .Should().Be(MigrationRequestGuard.NeedsDryRun);
    }

    [Fact]
    public void Window_line_is_hidden_when_the_summary_already_says_window()
    {
        MigrationDryRunText.ShowWindowLine("Window about 5 minutes, alert at 2.").Should().BeFalse();
        MigrationDryRunText.ShowWindowLine("Dry run accepted.").Should().BeTrue();
        MigrationDryRunText.ShowWindowLine(" ").Should().BeTrue();
    }

    [Fact]
    public void Eligibility_follows_the_dry_run_for_that_topic()
    {
        var refused = new PartitionPlan
        {
            Topic = "ReadyToAcquire",
            Accepted = false,
            Errors = ["The quiet window is not met."]
        };
        KafkaIncreaseEligibility.Line("Eligible for an in-place increase.", refused, "ReadyToAcquire")
            .Should().Be("Not eligible for an in-place increase.");
        KafkaIncreaseEligibility.Line("Not checked yet. Dry run an in-place increase to confirm it is eligible.", null, "ReadyToAcquire")
            .Should().Be("Not checked yet. Dry run an in-place increase to confirm it is eligible.");

        var family = new PartitionPlan
        {
            Topic = "ReadyToAcquire",
            Accepted = false,
            FamilyCompletion = true,
            Errors = ["Sibling refused."]
        };
        KafkaIncreaseEligibility.Line("Eligible for an in-place increase.", family, "ReadyToAcquire")
            .Should().Be("Eligible for an in-place increase.");

        KafkaIncreaseEligibility.Line("Eligible for an in-place increase.", refused, "Other")
            .Should().Be("Eligible for an in-place increase.");

        KafkaIncreaseEligibility.MigrationLine(
                "Eligible for an increase migration.",
                new KafkaMigrationPlan { Accepted = false, Errors = ["Group X is inactive and has lag."] })
            .Should().Be("Not eligible for a migration.");
        KafkaIncreaseEligibility.MigrationLine("Eligible for an increase migration.", null)
            .Should().Be("Eligible for an increase migration.");
    }

    [Fact]
    public void Produce_rate_says_measuring_until_it_is_known()
    {
        KafkaProduceRate.Text(false, 0).Should().Be("measuring");
        KafkaProduceRate.Text(true, 0).Should().Be("0");
        KafkaProduceRate.Text(true, 1.5).Should().Be("1.5");
    }

    [Fact]
    public void Chart_omits_rates_that_are_still_measuring()
    {
        var page = new ThroughputKafkaPage
        {
            ChartTopics =
            [
                new KafkaTopicRow { Topic = "measuring", ProduceRateKnown = false, ProduceRatePerSecond = 0, TotalLag = 4 },
                new KafkaTopicRow { Topic = "ready", ProduceRateKnown = true, ProduceRatePerSecond = 2, TotalLag = 1 }
            ]
        };

        using var json = JsonDocument.Parse(page.ChartJson);
        json.RootElement.GetProperty("rates").GetArrayLength().Should().Be(1);
        json.RootElement.GetProperty("rates")[0].GetProperty("label").GetString().Should().Be("ready");
        json.RootElement.GetProperty("lags").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void Log_dirs_stay_hidden_when_the_client_cannot_describe_them()
    {
        KafkaLogDirs.Show(new ClusterSnapshot
        {
            LogDirsAvailable = false,
            Brokers = [new BrokerSnapshot { LogDirBytes = -1 }]
        }).Should().BeFalse();
        KafkaLogDirs.Show(new ClusterSnapshot
        {
            LogDirsAvailable = true,
            Brokers = [new BrokerSnapshot { LogDirBytes = -1 }]
        }).Should().BeTrue();
        KafkaLogDirs.Show(new ClusterSnapshot
        {
            Brokers = [new BrokerSnapshot { LogDirBytes = 10 }]
        }).Should().BeTrue();
    }

    [Fact]
    public void Move_preview_groups_topics_labels_internal_ones_and_folds()
    {
        var moves = Enumerable.Range(0, 9)
            .Select(index => new ReplicaMove { Topic = "topic-" + index, Partition = 0 })
            .Append(new ReplicaMove { Topic = "_linkmig-journal", Partition = 0 })
            .Append(new ReplicaMove { Topic = "_linkmig-journal", Partition = 1 })
            .ToList();

        var lines = BrokerMovePreview.Lines(moves);
        lines.Should().HaveCount(10);
        lines.Take(9).Should().OnlyContain(line => !line.Internal);
        lines.Last().Topic.Should().Be("_linkmig-journal");
        lines.Last().Partitions.Should().Be(2);
        lines.Last().Internal.Should().BeTrue();
        lines.Skip(BrokerMovePreview.FoldAfter).Should().HaveCount(2);
    }

    [Fact]
    public void Console_views_keep_the_fix_markers_and_the_existing_button_classes()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        var root = Path.Combine(dir!.FullName, "DotNet", "Link.UI");
        var migrate = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Migrate.cshtml"));
        var topic = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Topic.cshtml"));
        var consumers = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Consumers.cshtml"));
        var brokers = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Brokers.cshtml"));
        var overview = File.ReadAllText(Path.Combine(root, "Views", "Operations", "_Overview.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "wwwroot", "js", "kafka-ops.js"));

        migrate.Should().Contain("name=\"acknowledgedGroups\"");
        migrate.Should().Contain("name=\"hasDryRunSnapshot\"");
        migrate.Should().Contain("id=\"kafka-dry-run-result\"");
        migrate.Should().Contain("KafkaIncreaseEligibility.MigrationLine");
        migrate.Should().Contain("data-kafka-busy=\"Dry run is running.\"");
        migrate.Should().Contain("class=\"btn btn-au-execute\" type=\"submit\" formaction=\"/Operations/Kafka/migrations/plan\">Dry run");
        migrate.Should().Contain("class=\"btn btn-success\"");
        migrate.Should().Contain("class=\"btn btn-warning\"");
        migrate.Should().Contain("plan.Errors");

        topic.Should().Contain("KafkaIncreaseEligibility.Line");
        topic.Should().Contain("id=\"kafka-partition-result\"");
        topic.Should().Contain("Preview increase");
        topic.Should().Contain("Preview family");
        topic.Should().Contain("class=\"btn btn-sm btn-au-execute lu-icon-btn\"");
        topic.Should().NotContain("btn-au-link lu-icon-btn");

        consumers.Should().Contain("This group has no members.");
        consumers.Should().Contain("This group has no committed partitions.");
        consumers.Should().Contain("Preview add");
        consumers.Should().Contain("Preview remove");
        consumers.Should().Contain("class=\"btn btn-sm btn-au-execute lu-icon-btn\"");
        consumers.Should().NotContain("btn-au-link lu-icon-btn");
        consumers.Should().Contain("Unknown");
        consumers.Should().Contain("selected.Catalogued");
        consumers.Should().Contain("Replica changes are hidden for an unknown group.");

        brokers.Should().Contain("KafkaLogDirs.Show");
        brokers.Should().Contain("BrokerMovePreview.Lines");
        brokers.Should().Contain("Show the rest");
        brokers.Should().Contain("(internal)");
        brokers.Should().Contain("name=\"addBrokerReason\"");
        brokers.Should().Contain("Preview decommission");
        brokers.Should().Contain("Preview rebalance");
        brokers.Should().Contain("class=\"btn btn-sm btn-au-execute lu-icon-btn\"");
        brokers.Should().NotContain("btn-au-link lu-icon-btn");
        brokers.Should().Contain("id=\"kafka-broker-result\"");

        overview.Should().Contain("KafkaProduceRate.Text");
        overview.Should().Contain("KafkaControllerTile.Display");
        File.ReadAllText(Path.Combine(dir!.FullName, "DotNet", "Link.UI", "Controllers", "OperationsController.cs"))
            .Should().Contain("KafkaPlanBanner.BesidePlan");
        overview.Should().NotContain("?? \"—\"");
        overview.Should().Contain("Unknown");

        script.Should().Contain("data-kafka-busy");
        script.Should().Contain("indexOf(\"/plan\")");
        script.Should().Contain("setTimeout");
        script.Should().Contain("button.disabled = true");
        script.Should().Contain("data-kafka-result");
        script.Should().NotContain("rows.map(function (_, index)");

        var labeled = File.ReadAllText(Path.Combine(root, "Views", "Shared", "_LabeledId.cshtml"));
        labeled.Should().Contain("LabeledIdRules.ShowsCopy");
        LabeledIdRules.ShowsCopy("Facility", "ReadyToAcquire").Should().BeFalse();
        LabeledIdRules.ShowsCopy("Facility", "6C5466DBD46746A1B89E2A9ADC72B0F7").Should().BeTrue();
        File.ReadAllText(Path.Combine(root, "Views", "Reports", "Index.cshtml")).Should().NotContain("Copy measures");
        File.ReadAllText(Path.Combine(root, "Views", "Tenants", "_ViewReports.cshtml")).Should().NotContain("Copy measures");
    }
}
