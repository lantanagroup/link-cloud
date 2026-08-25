using Automation.UI.Controllers.Api;
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
    public async Task GetDetail_returns_null_when_run_missing()
    {
        var presenter = CreatePresenter(run: null, snapshot: null);

        var detail = await presenter.GetDetailAsync(Guid.NewGuid());

        detail.Should().BeNull();
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
    public async Task ListAsync_returns_empty_records_and_metadata()
    {
        var store = new Mock<IRunMetricsStore>();
        store.Setup(s => s.ListPageAsync(1, 20, It.IsAny<CancellationToken>()))
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
        store.Setup(s => s.ListPageAsync(1, 20, It.IsAny<CancellationToken>()))
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
                ["dispatch"] = new StageLatencySnapshot { Unavailable = true },
                ["normalization"] = new StageLatencySnapshot { Unavailable = true },
                ["measureeval"] = new StageLatencySnapshot { Unavailable = true },
                ["validation"] = new StageLatencySnapshot { Unavailable = true },
                ["submission"] = new StageLatencySnapshot { Unavailable = true }
            }
        };
    }
}
