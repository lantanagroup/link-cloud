using System.Net;
using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Models;
using Link.UI.Controllers;
using Link.UI.Models;
using Link.UI.Services;
using Link.UI.Services.ReportBatches;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Link.UI.Tests;

public class ReportBatchLaunchTests
{
    [Fact]
    public async Task The_launch_page_defaults_copies_to_one_and_concurrency_to_the_saved_cap()
    {
        var harness = new LaunchHarness();
        var adhoc = harness.AddScenario("alpha", ReportMethod.Adhoc);
        var scheduled = harness.AddScenario("beta", ReportMethod.ScheduledReport);
        var regenerate = harness.AddScenario("gamma", ReportMethod.RegenerateReport);

        var model = Page(await harness.Controller().Index(CancellationToken.None));
        model.Ready.Should().BeTrue();
        model.Copies.Should().Be(1);
        model.MaxConcurrency.Should().Be(2);
        model.SavedCap.Should().Be(2);
        model.SelectedEhr.Should().Be("local");
        model.EhrSystems.Should().ContainSingle();
        model.EhrSystems[0].Name.Should().Be("local");
        model.EhrSystems[0].Ready.Should().BeTrue();
        model.Scenarios.Select(scenario => scenario.Id).Should().Equal(adhoc.Id, scheduled.Id, regenerate.Id);
        model.Scenarios.Select(scenario => scenario.Group).Should().Equal("Ad-hoc", "Scheduled", "Scheduled");
        model.Scenarios.Single(scenario => scenario.Id == scheduled.Id).ReportMethodLabel.Should().Be("Scheduled report");
        model.Scenarios.Single(scenario => scenario.Id == regenerate.Id).Group.Should().Be("Scheduled");

        await harness.Settings.SaveMaxConcurrencyAsync(4);
        Page(await harness.Controller().Index(CancellationToken.None)).MaxConcurrency.Should().Be(4);
    }

    [Fact]
    public async Task Starting_a_batch_repeats_each_scenario_and_keeps_the_lowered_cap()
    {
        var harness = new LaunchHarness();
        var scheduled = harness.AddScenario("scheduled", ReportMethod.ScheduledReport);
        var adhoc = harness.AddScenario("adhoc", ReportMethod.Adhoc);

        var result = await harness.Controller().Index(
            "local",
            [scheduled.Id, adhoc.Id],
            copies: 2,
            maxConcurrency: 1,
            CancellationToken.None);

        var batch = harness.Batches.Only();
        result.Should().BeOfType<RedirectResult>().Which.Url
            .Should().Be("/Automation/ReportBatches/" + batch.BatchId.ToString("D"));
        batch.EhrSystem.Should().Be("local");
        batch.MaxConcurrency.Should().Be(1);
        batch.Items.Select(item => item.ScenarioId).Should().Equal(scheduled.Id, scheduled.Id, adhoc.Id, adhoc.Id);
        var started = harness.Runs.Started.Should().ContainSingle().Subject;
        started.EhrSystem.Should().Be("local");
        started.ReportMethod.Should().Be(ReportMethod.ScheduledReport);
        (await harness.Settings.GetMaxConcurrencyAsync()).Should().Be(2);
    }

    [Fact]
    public async Task An_unknown_EHR_is_rejected()
    {
        var harness = new LaunchHarness();
        var scenario = harness.AddScenario("alpha");

        var model = await Rejected(harness, "other", [scenario.Id], 1, 1);
        model.Errors.Should().Contain("ehrSystem is required and must be a configured EHR system.");
    }

    [Fact]
    public async Task A_not_ready_EHR_is_rejected()
    {
        var harness = new LaunchHarness(HttpStatusCode.ServiceUnavailable);
        var scenario = harness.AddScenario("alpha");

        var model = await Rejected(harness, "local", [scenario.Id], 1, 1);
        model.Errors.Should().Contain(error => error.Contains("not ready", StringComparison.Ordinal) && error.Contains("503", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_missing_scenario_is_rejected()
    {
        var harness = new LaunchHarness();
        var missing = Guid.NewGuid();

        var model = await Rejected(harness, "local", [missing], 1, 1);
        model.Errors.Should().Contain($"Scenario {missing} was not found.");
    }

    [Fact]
    public async Task Too_many_copies_is_rejected()
    {
        var harness = new LaunchHarness();
        var scenario = harness.AddScenario("alpha");

        var model = await Rejected(harness, "local", [scenario.Id], 101, 1);
        model.Errors.Should().Contain("A batch can include at most 100 scenarios.");
        model.Copies.Should().Be(101);
    }

    [Fact]
    public async Task A_cap_above_the_saved_cap_is_rejected()
    {
        var harness = new LaunchHarness();
        var scenario = harness.AddScenario("alpha");

        var model = await Rejected(harness, "local", [scenario.Id], 1, 4);
        model.Errors.Should().Contain("maxConcurrency cannot be above the saved cap of 2.");
        (await harness.Settings.GetMaxConcurrencyAsync()).Should().Be(2);
    }

    [Fact]
    public async Task A_scenario_without_a_period_or_patients_is_rejected()
    {
        var harness = new LaunchHarness();
        var scenario = harness.AddScenario("alpha");
        scenario.ReportPeriodStart = null;
        scenario.ReportPeriodEnd = null;

        var missingPeriod = await Rejected(harness, "local", [scenario.Id], 1, 1);
        missingPeriod.Errors.Should().Contain("alpha needs a report period.");

        scenario.ReportPeriodStart = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);
        scenario.ReportPeriodEnd = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var reversed = await Rejected(harness, "local", [scenario.Id], 1, 1);
        reversed.Errors.Should().Contain("alpha has a report period that starts after it ends.");

        scenario.ReportPeriodEnd = new DateTimeOffset(2024, 2, 28, 0, 0, 0, TimeSpan.Zero);
        scenario.PatientCount = 0;
        var missingPatients = await Rejected(harness, "local", [scenario.Id], 1, 1);
        missingPatients.Errors.Should().Contain("alpha needs a patient source.");
    }

    [Fact]
    public async Task The_page_stays_closed_until_the_engine_is_ready()
    {
        var harness = new LaunchHarness(HttpStatusCode.OK, engineReady: false);

        var model = Page(await harness.Controller().Index(CancellationToken.None));
        model.Ready.Should().BeFalse();
        model.Errors.Should().Contain(ReportBatchLaunchController.Unavailable);
        model.EhrSystems.Should().ContainSingle().Which.Ready.Should().BeTrue();

        var posted = Page(await harness.Controller().Index("local", [Guid.NewGuid()], 1, 1, CancellationToken.None));
        posted.Ready.Should().BeFalse();
        posted.Errors.Should().Contain(ReportBatchLaunchController.Unavailable);
    }

    [Fact]
    public void The_launch_page_is_on_the_nav_and_the_scenario_label_is_renamed()
    {
        var launch = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/ReportBatchLaunch/Index.cshtml"));
        launch.Should().Contain("name=\"ehrSystem\"");
        launch.Should().Contain("name=\"scenarioIds\"");
        launch.Should().Contain("name=\"copies\"");
        launch.Should().Contain("name=\"maxConcurrency\"");
        launch.Should().Contain("Start batch");
        launch.Should().Contain("Edit scenarios");

        var nav = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Automation/_Nav.cshtml"));
        nav.Should().Contain("asp-controller=\"ReportBatchLaunch\"");
        nav.Should().Contain("asp-controller=\"ReportBatchSettings\"");

        var settings = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/ReportBatchSettings/Index.cshtml"));
        settings.Should().Contain("Launch a batch");

        var editor = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_ScenarioEditorModal.cshtml"));
        editor.Should().Contain("FHIR query concurrency (1–8)");
        editor.Should().Contain("FHIR query concurrency must be between 1 and 8.");
        editor.Should().NotContain("FHIR query parallelism");

        var automationEditor = File.ReadAllText(RepoFile("DotNet/Automation.UI/Views/Shared/_ScenarioEditorModal.cshtml"));
        automationEditor.Should().Contain("FHIR query parallelism");
    }

    private static ReportBatchLaunchPage Page(IActionResult result)
        => result.Should().BeOfType<ViewResult>().Which.Model.Should().BeOfType<ReportBatchLaunchPage>().Subject;

    private static async Task<ReportBatchLaunchPage> Rejected(
        LaunchHarness harness,
        string ehrSystem,
        List<Guid> scenarioIds,
        int copies,
        int maxConcurrency)
    {
        var model = Page(await harness.Controller().Index(
            ehrSystem,
            scenarioIds,
            copies,
            maxConcurrency,
            CancellationToken.None));
        harness.Batches.Count.Should().Be(0);
        return model;
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "link-cloud.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class LaunchHarness
    {
        public LaunchHarness(HttpStatusCode probeStatus = HttpStatusCode.OK, bool engineReady = true)
        {
            Settings = new MemorySettings();
            Scenarios = new MemoryScenarios();
            Batches = new MemoryBatches();
            Runs = new FakeRuns();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Automation:EhrSystems:0:Name"] = "local",
                ["Automation:EhrSystems:0:FhirServerBase"] = "http://127.0.0.1:9/fhir",
                ["Automation:EhrSystems:0:IsDefault"] = "true"
            }).Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IOptionsMonitor<AutomationConfig>>(new FixedMonitor(new AutomationConfig()));
            services.AddSingleton<EhrSystemCatalog>();
            services.AddHttpClient(EhrSystemReadinessChecker.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new StatusHandler(probeStatus));
            services.AddSingleton<EhrSystemReadinessChecker>();
            services.AddSingleton<IScenarioStore>(Scenarios);
            services.AddSingleton<IReportBatchSettingsStore>(Settings);
            if (engineReady)
            {
                Dispatcher = new ReportBatchDispatcher(
                    Batches,
                    Settings,
                    Scenarios,
                    new FakeEhr(),
                    Runs,
                    TimeProvider.System,
                    NullLogger<ReportBatchDispatcher>.Instance);
                services.AddSingleton(Dispatcher);
            }

            Services = services.BuildServiceProvider();
        }

        public MemorySettings Settings { get; }
        public MemoryScenarios Scenarios { get; }
        public MemoryBatches Batches { get; }
        public FakeRuns Runs { get; }
        public ReportBatchDispatcher? Dispatcher { get; }
        public ServiceProvider Services { get; }

        public ReportBatchLaunchController Controller() => new(Services);

        public TestScenarioDefinition AddScenario(string name, ReportMethod method = ReportMethod.Adhoc)
        {
            var scenario = new TestScenarioDefinition
            {
                Name = name,
                ReportMethod = method,
                PatientCount = 2,
                ReportPeriodStart = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ReportPeriodEnd = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero)
            };
            Scenarios.Items[scenario.Id] = scenario;
            return scenario;
        }
    }

    private sealed class FixedMonitor(AutomationConfig value) : IOptionsMonitor<AutomationConfig>
    {
        public AutomationConfig CurrentValue => value;

        public AutomationConfig Get(string? name) => value;

        public IDisposable? OnChange(Action<AutomationConfig, string?> listener) => null;
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status));
    }

    private sealed class FakeEhr : IReportBatchEhrLookup
    {
        public string? CanonicalName(string? requestedName)
        {
            var trimmed = requestedName?.Trim();
            return string.Equals(trimmed, "local", StringComparison.OrdinalIgnoreCase) ? "local" : null;
        }
    }

    private sealed class MemorySettings : IReportBatchSettingsStore
    {
        private int? _saved;

        public Task<int> GetMaxConcurrencyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_saved is int value && ReportBatchConcurrencyPolicy.Validate(value) is null
                ? value
                : ReportBatchConcurrencyPolicy.DefaultMaxConcurrency);

        public Task SaveMaxConcurrencyAsync(int maxConcurrency, CancellationToken cancellationToken = default)
        {
            var error = ReportBatchConcurrencyPolicy.Validate(maxConcurrency);
            if (error != null)
                throw new InvalidOperationException(error);

            _saved = maxConcurrency;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryScenarios : IScenarioStore
    {
        public Dictionary<Guid, TestScenarioDefinition> Items { get; } = [];

        public Task<List<TestScenarioDefinition>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(Items.Values.ToList());

        public Task<TestScenarioDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.GetValueOrDefault(id));

        public Task UpsertAsync(TestScenarioDefinition scenario, CancellationToken ct = default)
        {
            Items[scenario.Id] = scenario;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryBatches : IReportBatchStore
    {
        private readonly Dictionary<Guid, ReportBatchDocument> _batches = [];

        public int Count => _batches.Count;

        public ReportBatchDocument Only() => _batches.Values.Single();

        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task InsertAsync(ReportBatchDocument batch, CancellationToken cancellationToken = default)
        {
            _batches[batch.BatchId] = batch;
            return Task.CompletedTask;
        }

        public Task<ReportBatchDocument?> GetAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(_batches.GetValueOrDefault(batchId));

        public Task ReplaceAsync(ReportBatchDocument batch, CancellationToken cancellationToken = default)
        {
            _batches[batch.BatchId] = batch;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ReportBatchDocument>> ListActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReportBatchDocument>>(_batches.Values.ToList());
    }

    private sealed class FakeRuns : IReportBatchRunGateway
    {
        public List<StartScenarioRequest> Started { get; } = [];

        public Task<Guid> StartAsync(StartScenarioRequest request, CancellationToken cancellationToken = default)
        {
            Started.Add(request);
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<bool> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<AutomationRunSummary?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult<AutomationRunSummary?>(null);
    }
}
