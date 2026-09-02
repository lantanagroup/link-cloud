using Automation.UI.Controllers.Api;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class MetricsRunPresenterTests
{
    [Fact]
    public void ToListItem_marks_stages_unavailable_when_all_missing()
    {
        var doc = Document();
        doc.Stages["acquisition"] = new StageLatencySnapshot { Unavailable = true };

        var item = MetricsRunPresenter.ToListItem(doc);

        item.StagesUnavailable.Should().BeTrue();
        item.E2eDurationSeconds.Should().Be(90);
        item.BenchmarkPass.Should().BeTrue();
    }

    [Fact]
    public void ToDetail_copies_quantiles_when_stage_is_available()
    {
        var doc = Document();
        doc.Stages["acquisition"] = new StageLatencySnapshot
        {
            Unavailable = false,
            Count = 8,
            P50Ms = 100,
            P95Ms = 1234,
            P99Ms = 2000
        };

        var detail = MetricsRunPresenter.ToDetail(doc);

        detail.StagesUnavailable.Should().BeFalse();
        detail.Stages["acquisition"].Unavailable.Should().BeFalse();
        detail.Stages["acquisition"].Count.Should().Be(8);
        detail.Stages["acquisition"].P95Ms.Should().Be(1234);
        detail.PatientsPerMinute.Should().Be(10);
    }

    [Fact]
    public async Task GetDetail_returns_null_when_run_and_snapshot_are_missing()
    {
        var presenter = CreatePresenter(run: null, snapshot: null);

        var detail = await presenter.GetDetailAsync(Guid.NewGuid());

        detail.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSnapshot_deletes_metrics_row_only()
    {
        var runId = Guid.NewGuid();
        var store = new Mock<IRunMetricsStore>();
        store.Setup(s => s.DeleteAsync(runId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var manager = new Mock<IAutomationRunManager>(MockBehavior.Strict);
        var presenter = new MetricsRunPresenter(store.Object, manager.Object, Mock.Of<IScenarioStore>());

        var deleted = await presenter.DeleteSnapshotAsync(runId);

        deleted.Should().BeTrue();
        store.Verify(s => s.DeleteAsync(runId, It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(m => m.DeleteRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDetail_returns_snapshot_when_run_was_deleted()
    {
        var snapshot = Document();
        var presenter = CreatePresenter(run: null, snapshot);

        var detail = await presenter.GetDetailAsync(snapshot.RunId);

        detail.Should().NotBeNull();
        detail!.RunId.Should().Be(snapshot.RunId);
        detail.RunAvailable.Should().BeFalse();
        detail.E2eDurationSeconds.Should().Be(90);
        detail.ScenarioName.Should().Be("perf-scenario");
    }

    [Fact]
    public async Task GetDetail_returns_unavailable_when_snapshot_missing()
    {
        var runId = Guid.NewGuid();
        var run = new AutomationRunSummary
        {
            RunId = runId,
            RunName = "perf-scenario",
            Status = AutomationRunStatus.Succeeded,
            PatientCount = 10,
            Seed = 7,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            FinishedAt = DateTimeOffset.UtcNow,
            IsMetricsRun = true
        };
        var presenter = CreatePresenter(run, snapshot: null);

        var detail = await presenter.GetDetailAsync(runId);

        detail.Should().NotBeNull();
        detail!.StagesUnavailable.Should().BeTrue();
        detail.RunAvailable.Should().BeTrue();
        detail.Stages.Values.Should().OnlyContain(s => s.Unavailable);
        detail.PatientCount.Should().Be(10);
        detail.E2eDurationSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetDetail_attaches_previous_succeeded_run()
    {
        var scenarioId = Guid.NewGuid();
        var previousId = Guid.NewGuid();
        var snapshot = Document(scenarioId);
        snapshot.Outcome = "Succeeded";
        var previous = Document(scenarioId);
        previous.RunId = previousId;
        previous.Outcome = "Succeeded";

        var store = new Mock<IRunMetricsStore>();
        store.Setup(s => s.GetAsync(snapshot.RunId, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        store.Setup(s => s.GetPreviousSucceededAsync(scenarioId, snapshot.FinishedAt, snapshot.RunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previous);
        var manager = new Mock<IAutomationRunManager>();
        manager.Setup(m => m.GetRunAsync(snapshot.RunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRunSummary { RunId = snapshot.RunId, Status = AutomationRunStatus.Succeeded });

        var presenter = new MetricsRunPresenter(store.Object, manager.Object, Mock.Of<IScenarioStore>());
        var detail = await presenter.GetDetailAsync(snapshot.RunId);

        detail!.PreviousRunId.Should().Be(previousId);
    }

    [Fact]
    public async Task GetDashboard_groups_runs_into_scenario_cards()
    {
        var scenarioId = Guid.NewGuid();
        var older = Document(scenarioId);
        older.FinishedAt = DateTimeOffset.UtcNow.AddDays(-2);
        older.E2eDurationSeconds = 80;
        var newer = Document(scenarioId);
        newer.FinishedAt = DateTimeOffset.UtcNow.AddHours(-1);
        newer.E2eDurationSeconds = 90;
        newer.Regression = new RegressionResultSnapshot { Flags = ["slower"] };

        var store = new Mock<IRunMetricsStore>();
        store.Setup(s => s.ListPageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { newer, older }.ToList(), 2L));
        store.Setup(s => s.ListSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { newer, older });
        var scenarios = new Mock<IScenarioStore>();
        scenarios.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestScenarioDefinition { Id = scenarioId, Name = "Monthly 150", IsMetricsRun = true }]);

        var presenter = new MetricsRunPresenter(store.Object, Mock.Of<IAutomationRunManager>(), scenarios.Object);
        var dashboard = await presenter.GetDashboardAsync(1, 20);

        dashboard.ScenarioCards.Should().ContainSingle();
        dashboard.ScenarioCards[0].Name.Should().Be("Monthly 150");
        dashboard.ScenarioCards[0].RunCount.Should().Be(2);
        dashboard.ScenarioCards[0].GotSlower.Should().BeTrue();
        dashboard.ScenarioCards[0].LastE2eSeconds.Should().Be(90);
        dashboard.Services.Should().HaveCount(5);
        dashboard.Services.Select(s => s.Key).Should().Equal(
            "acquisition", "normalization", "measureeval", "validation", "submission");
    }

    [Fact]
    public async Task GetDashboard_groups_metrics_without_scenario_id_by_matching_name()
    {
        var scenarioId = Guid.NewGuid();
        var orphan = Document();
        orphan.ScenarioId = null;
        orphan.ScenarioName = "Monthly 150";
        orphan.E2eDurationSeconds = 77;

        var store = new Mock<IRunMetricsStore>();
        store.Setup(s => s.ListPageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { orphan }.ToList(), 1L));
        store.Setup(s => s.ListSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { orphan });
        var scenarios = new Mock<IScenarioStore>();
        scenarios.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestScenarioDefinition { Id = scenarioId, Name = "Monthly 150", IsMetricsRun = true }]);

        var presenter = new MetricsRunPresenter(store.Object, Mock.Of<IAutomationRunManager>(), scenarios.Object);
        var dashboard = await presenter.GetDashboardAsync(1, 20);

        dashboard.ScenarioCards.Should().ContainSingle();
        dashboard.ScenarioCards[0].ScenarioId.Should().Be(scenarioId);
        dashboard.ScenarioCards[0].Name.Should().Be("Monthly 150");
        dashboard.ScenarioCards[0].RunCount.Should().Be(1);
        dashboard.ScenarioCards[0].LastE2eSeconds.Should().Be(77);
    }

    [Fact]
    public async Task ListAsync_returns_empty_records_and_metadata()
    {
        var store = new Mock<IRunMetricsStore>();
        store.Setup(s => s.ListPageAsync(1, 20, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<AutomationRunMetricsDocument>(), 0L));
        var presenter = new MetricsRunPresenter(store.Object, Mock.Of<IAutomationRunManager>(), Mock.Of<IScenarioStore>());

        var (records, metadata) = await presenter.ListAsync(1, 20);

        records.Should().BeEmpty();
        metadata.TotalCount.Should().Be(0);
        metadata.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task Api_list_metrics_returns_records_and_metadata()
    {
        var store = new Mock<IRunMetricsStore>();
        store.Setup(s => s.ListPageAsync(1, 20, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<AutomationRunMetricsDocument>(), 0L));
        var presenter = new MetricsRunPresenter(store.Object, Mock.Of<IAutomationRunManager>(), Mock.Of<IScenarioStore>());
        var controller = new AutomationRunsApiController(
            Mock.Of<IAutomationRunManager>(),
            Mock.Of<IScenarioStore>(),
            presenter,
            NullLogger<AutomationRunsApiController>.Instance);

        var result = await controller.ListMetrics(1, 20, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PutBenchmark_rejects_key_mismatch()
    {
        var presenter = CreatePresenter(run: null, snapshot: null);
        var controller = new AutomationRunsApiController(
            Mock.Of<IAutomationRunManager>(),
            Mock.Of<IScenarioStore>(),
            presenter,
            NullLogger<AutomationRunsApiController>.Instance);

        var result = await controller.PutBenchmark(
            "nhsn-monthly-150",
            new AutomationMetricsBenchmarkDocument { Key = "other" },
            CancellationToken.None);

        var problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Api_get_metrics_returns_problem_when_run_missing()
    {
        var presenter = CreatePresenter(run: null, snapshot: null);
        var controller = new AutomationRunsApiController(
            Mock.Of<IAutomationRunManager>(),
            Mock.Of<IScenarioStore>(),
            presenter,
            NullLogger<AutomationRunsApiController>.Instance);

        var result = await controller.GetMetrics(Guid.NewGuid(), CancellationToken.None);

        var problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(404);
    }

    private static MetricsRunPresenter CreatePresenter(AutomationRunSummary? run, AutomationRunMetricsDocument? snapshot)
    {
        var store = new Mock<IRunMetricsStore>();
        store.Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        var manager = new Mock<IAutomationRunManager>();
        manager.Setup(m => m.GetRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);
        return new MetricsRunPresenter(store.Object, manager.Object, Mock.Of<IScenarioStore>());
    }

    private static AutomationRunMetricsDocument Document(Guid? scenarioId = null)
    {
        return new AutomationRunMetricsDocument
        {
            RunId = Guid.NewGuid(),
            ScenarioId = scenarioId ?? Guid.NewGuid(),
            ScenarioName = "perf-scenario",
            Outcome = "Succeeded",
            FinishedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-90),
            E2eDurationSeconds = 90,
            PatientCount = 15,
            Throughput = new ThroughputSnapshot { PatientsPerMinute = 10, ResourcesPerSecond = 2 },
            Thetis = new ThetisRevisionSnapshot { Seed = 20260329, GitSha = "abc123" },
            Stages = new Dictionary<string, StageLatencySnapshot>(StringComparer.Ordinal)
            {
                ["acquisition"] = new StageLatencySnapshot { Unavailable = true },
                ["normalization"] = new StageLatencySnapshot { Unavailable = true },
                ["measureeval"] = new StageLatencySnapshot { Unavailable = true },
                ["validation"] = new StageLatencySnapshot { Unavailable = true },
                ["submission"] = new StageLatencySnapshot { Unavailable = true }
            }
        };
    }
}
