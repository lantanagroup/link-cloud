using Automation.UI.Services.Persistence;
using Automation.UI.Services.TestRail;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class TestRailRunLinkMergeTests
{
    [Fact]
    public void Summary_update_does_not_replace_the_stored_run_list()
    {
        var summary = Summary(Link(11), Link(12));

        var rendered = Render(MongoSnapshotStore.BuildRunSummaryUpdate(summary, summary.FacilityId, summary.ReportId));

        rendered.ToString().Should().NotContain("TestRailRuns");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Empty_or_unusable_links_produce_no_merge(bool emptyList)
    {
        var runs = emptyList
            ? []
            : new List<TestRailRunReference> { new() { RunId = 11, Url = "javascript:alert(1)" } };

        MongoSnapshotStore.BuildTestRailRunMerges(Guid.NewGuid(), runs).Should().BeEmpty();
        MongoSnapshotStore.BuildTestRailRunMerges(Guid.NewGuid(), null).Should().BeEmpty();
    }

    [Fact]
    public void Each_run_id_is_added_with_addToSet_each_only_when_that_id_is_absent()
    {
        var runId = Guid.NewGuid();
        var merges = MongoSnapshotStore.BuildTestRailRunMerges(runId, [Link(11), Link(12)]);

        merges.Should().HaveCount(2);
        foreach (var merge in merges)
        {
            merge.Options.Should().BeNull();
            var update = Render(merge.Update).ToString();
            update.Should().Contain("$addToSet");
            update.Should().NotContain("$set");
        }

        var filters = merges.Select(merge => Render(merge.Filter).ToString()).ToList();
        filters.Should().AllSatisfy(filter => filter.Should().Contain("$elemMatch"));
        string.Join('\n', filters).Should().Contain("11").And.Contain("12");
    }

    [Fact]
    public void The_same_run_id_is_merged_once_and_a_test_link_fills_the_stored_row()
    {
        var withoutTest = TestRailRunLinks.Capture(
            "https://example.testrail.io",
            501,
            [
                new TestRailResultDto { Id = 1, TestId = 10 },
                new TestRailResultDto { Id = 2, TestId = 11 }
            ])!;
        var withTest = TestRailRunLinks.Capture(
            "https://example.testrail.io",
            501,
            [new TestRailResultDto { Id = 3, TestId = 16898 }])!;

        var merges = MongoSnapshotStore.BuildTestRailRunMerges(Guid.NewGuid(), [withoutTest, withTest]);

        merges.Should().HaveCount(2);
        Render(merges[0].Update).ToString().Should().Contain("$addToSet");
        var upgrade = Render(merges[1].Update).ToString();
        upgrade.Should().Contain("TestRailRuns.$[link].TestId");
        upgrade.Should().Contain("16898");
        upgrade.Should().NotContain("$addToSet");
        merges[1].Options.Should().NotBeNull();
        merges[1].Options!.ArrayFilters.Should().NotBeNull();
        merges[1].Options.ArrayFilters!.Should().ContainSingle();
    }

    private static AutomationRunSummary Summary(params TestRailRunReference[] links) => new()
    {
        RunId = Guid.NewGuid(),
        RunName = "link-merge",
        Scenario = AutomationScenarioKind.Custom,
        Status = AutomationRunStatus.Succeeded,
        CreatedAt = DateTimeOffset.UtcNow,
        FacilityId = "facility",
        ReportId = "report",
        TestRailRuns = links.ToList()
    };

    private static TestRailRunReference Link(int runId) =>
        TestRailRunLinks.Capture(
            "https://example.testrail.io",
            runId,
            [new TestRailResultDto { Id = 1, TestId = 0 }])!;

    private static BsonValue Render(UpdateDefinition<AutomationRunDocument> update)
    {
        var registry = BsonSerializer.SerializerRegistry;
        var serializer = registry.GetSerializer<AutomationRunDocument>();
        return update.Render(new RenderArgs<AutomationRunDocument>(serializer, registry));
    }

    private static BsonValue Render(FilterDefinition<AutomationRunDocument> filter)
    {
        var registry = BsonSerializer.SerializerRegistry;
        var serializer = registry.GetSerializer<AutomationRunDocument>();
        return filter.Render(new RenderArgs<AutomationRunDocument>(serializer, registry));
    }
}
