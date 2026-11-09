using Automation.UI.Services.Persistence;
using Automation.UI.Services.TestRail;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.AutomationUI;

[Collection(IntegrationTestCollection.Name)]
public class MongoSnapshotStoreTestRailLinkTests : IAsyncLifetime
{
    private readonly AutomationUIIntegrationTestFixture _fixture;

    public MongoSnapshotStoreTestRailLinkTests(AutomationUIIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Overlapping_summaries_keep_every_run_id_and_an_empty_list_does_not_clear_them()
    {
        var store = _fixture.CreateStore();
        var runId = Guid.NewGuid();

        await store.UpsertRunSummaryAsync(Summary(runId, Link(11), Link(12)), "facility", "report");
        await store.UpsertRunSummaryAsync(Summary(runId, Link(11)), "facility", "report");
        await store.UpsertRunSummaryAsync(Summary(runId), "facility", "report");

        var stored = await store.GetRunSummaryAsync(runId);
        stored.Should().NotBeNull();
        stored!.TestRailRuns.Select(run => run.RunId).Should().Equal(11, 12);
    }

    [Fact]
    public async Task Parallel_writes_of_different_run_ids_keep_each_id_once()
    {
        var store = _fixture.CreateStore();
        var runId = Guid.NewGuid();
        await store.UpsertRunSummaryAsync(Summary(runId), "facility", "report");

        await Task.WhenAll(Enumerable.Range(1, 20).Select(id =>
            store.UpsertRunSummaryAsync(Summary(runId, Link(id)), "facility", "report")));

        var stored = await store.GetRunSummaryAsync(runId);
        stored!.TestRailRuns.Select(run => run.RunId).OrderBy(id => id).Should().Equal(Enumerable.Range(1, 20));
    }

    [Fact]
    public async Task A_later_test_link_updates_the_same_run_id_instead_of_adding_another_row()
    {
        var store = _fixture.CreateStore();
        var runId = Guid.NewGuid();
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

        await store.UpsertRunSummaryAsync(Summary(runId, withoutTest), "facility", "report");
        await store.UpsertRunSummaryAsync(Summary(runId, withTest), "facility", "report");

        var stored = await store.GetRunSummaryAsync(runId);
        stored!.TestRailRuns.Should().ContainSingle();
        stored.TestRailRuns[0].RunId.Should().Be(501);
        stored.TestRailRuns[0].TestId.Should().Be(16898);
        stored.TestRailRuns[0].TestUrl.Should().Be("https://example.testrail.io/index.php?/tests/view/16898");
    }

    private static AutomationRunSummary Summary(Guid runId, params TestRailRunReference[] links) => new()
    {
        RunId = runId,
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
}
