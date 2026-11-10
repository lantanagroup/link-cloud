using Automation.UI.Models;
using Automation.UI.Models.ApiHealth;
using Automation.UI.Services;
using Automation.UI.Services.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using Automation.UI.Services.ApiHealth.TestSuites;
using Automation.UI.Services.Persistence;
using Automation.UI.Services.TestRail;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.AutomationUI;

[Collection(IntegrationTestCollection.Name)]
public class TestRailPublishHookTests : IAsyncLifetime
{
    private readonly AutomationUIIntegrationTestFixture _fixture;

    public TestRailPublishHookTests(AutomationUIIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Scenario_run_completion_publishes_once()
    {
        var publisher = new RecordingTestRailPublisher();
        var measures = new Mock<IMeasureTemplateStore>();
        measures
            .Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Guid> ids, CancellationToken _) =>
                Task.FromResult(ids.Select(id => new MeasureTemplate { Id = id, Name = "stub" }).ToList()));

        var manager = _fixture.CreateRunManager(publisher, measures.Object);
        var runId = await manager.StartAsync(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            ScenarioName = "publish-once",
            FacilityConfigurationMode = FacilityConfigurationMode.Unspecified,
            PatientCount = 1,
            Seed = 1
        });

        await WaitUntilAsync(() => publisher.ScenarioCalls.Count > 0);

        publisher.ScenarioCalls.Should().ContainSingle();
        publisher.ScenarioCalls[0].RunId.Should().Be(runId);
        publisher.ScenarioCalls[0].Status.IsTerminal().Should().BeTrue();
        publisher.ApiHealthCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Stuck_run_recovery_publishes_once()
    {
        var scenarioId = Guid.NewGuid();
        var summary = new AutomationRunSummary
        {
            RunId = Guid.NewGuid(),
            RunName = "stuck",
            Scenario = AutomationScenarioKind.Custom,
            Status = AutomationRunStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow,
            FacilityId = "facility-stuck",
            ReportId = "report-stuck"
        };
        var store = _fixture.CreateStore();
        await store.UpsertRunSummaryAsync(summary, summary.FacilityId, summary.ReportId);
        await store.UpsertRunInputAsync(new AutomationRunInputSnapshot
        {
            RunId = summary.RunId,
            ScenarioId = scenarioId,
            ScenarioName = "mapped-scenario",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var publisher = new RecordingTestRailPublisher();
        var manager = _fixture.CreateRunManager(publisher);

        var cancelled = await manager.CancelRunAsync(summary.RunId);

        cancelled.Should().BeTrue();
        publisher.ScenarioCalls.Should().ContainSingle();
        publisher.ScenarioCalls[0].RunId.Should().Be(summary.RunId);
        publisher.ScenarioCalls[0].ScenarioId.Should().Be(scenarioId);
        publisher.ScenarioCalls[0].RunName.Should().Be("stuck");
        publisher.ScenarioCalls[0].Status.Should().Be(AutomationRunStatus.Cancelled);
    }

    [Fact]
    public async Task Api_health_completion_publishes_once()
    {
        var publisher = new RecordingTestRailPublisher();
        var manager = new ApiHealthExecutionRunManager(
            new ApiEndpointRegistry([new ProbeSuite()]),
            new ReadySeedOrchestrator(),
            new ApiHealthSeedContextAccessor(),
            new MongoApiHealthRunStore(_fixture.Database),
            new StubHttpClientFactory(),
            new ConfigurationBuilder().Build(),
            publisher,
            NullLogger<ApiHealthExecutionRunManager>.Instance);

        var runId = await manager.StartServiceAsync("Probe");

        await WaitUntilAsync(() => publisher.ApiHealthCalls.Count > 0);

        publisher.ApiHealthCalls.Should().ContainSingle();
        publisher.ApiHealthCalls[0].RunId.Should().Be(runId);
        publisher.ApiHealthCalls[0].Scope.Should().Be("Service");
        publisher.ApiHealthCalls[0].ServiceName.Should().Be("Probe");
        publisher.ApiHealthCalls[0].Failed.Should().BeFalse();
        publisher.ScenarioCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task TestRailCaseId_round_trips_through_mongo()
    {
        var store = new MongoScenarioStore(_fixture.Database, Mock.Of<IImportedBundleContentStore>());
        var scenario = new TestScenarioDefinition
        {
            Id = Guid.NewGuid(),
            Name = "rail-case",
            TestRailCaseId = 4242
        };

        await store.UpsertAsync(scenario);
        var loaded = await store.GetByIdAsync(scenario.Id);

        loaded.Should().NotBeNull();
        loaded!.TestRailCaseId.Should().Be(4242);

        scenario.TestRailCaseId = null;
        await store.UpsertAsync(scenario);
        var cleared = await store.GetByIdAsync(scenario.Id);
        cleared!.TestRailCaseId.Should().BeNull();
    }

    private static async Task WaitUntilAsync(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (done())
                return;
            await Task.Delay(50);
        }

        throw new TimeoutException("Timed out waiting for TestRail publish.");
    }

    private sealed class ProbeSuite : IServiceTestSuite
    {
        public string ServiceName => "Probe";

        public IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() => [];

        public Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ApiTestRunResult>>([]);
    }

    private sealed class ReadySeedOrchestrator : IApiHealthSeedOrchestrator
    {
        public Task<ApiHealthSeedSession> BeginServiceAsync(
            string serviceName,
            IReadOnlyCollection<ApiHealthSeedRequirement> requirements,
            Guid? apiHealthRunId = null,
            CancellationToken ct = default)
            => Task.FromResult(new ApiHealthSeedSession { Scope = $"Service:{serviceName}", Success = true });

        public Task<ApiHealthSeedSession> BeginAllAsync(
            IReadOnlyCollection<ApiHealthSeedRequirement> requirements,
            Guid? apiHealthRunId = null,
            CancellationToken ct = default)
            => Task.FromResult(new ApiHealthSeedSession { Scope = "All", Success = true });

        public Task EndAsync(ApiHealthSeedSession session, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> IsSeedRunCancelledAsync(ApiHealthSeedSession session, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler());

        private sealed class StubHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }
}
