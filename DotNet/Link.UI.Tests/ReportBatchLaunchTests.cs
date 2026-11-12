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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
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
        model.SavedCap.Should().Be(2);
        model.EhrSystems.Should().ContainSingle();
        model.EhrSystems[0].Name.Should().Be("local");
        model.EhrSystems[0].Selected.Should().BeTrue();
        model.EhrSystems[0].Copies.Should().Be(1);
        model.EhrSystems[0].MaxConcurrency.Should().Be(2);
        model.EhrSystems[0].FacilityTemplate.Should().Be("System default");
        model.EhrSystems[0].Ready.Should().BeTrue();
        model.Scenarios.Select(scenario => scenario.Id).Should().Equal(adhoc.Id, scheduled.Id, regenerate.Id);
        model.Scenarios.Select(scenario => scenario.Group).Should().Equal("Ad-hoc", "Scheduled", "Scheduled");
        model.Scenarios.Single(scenario => scenario.Id == adhoc.Id).ReportMethodLabel.Should().Be("Ad-hoc");
        model.Scenarios.Single(scenario => scenario.Id == scheduled.Id).ReportMethodLabel.Should().Be("Scheduled report");
        model.Scenarios.Single(scenario => scenario.Id == regenerate.Id).Group.Should().Be("Scheduled");

        await harness.Settings.SaveMaxConcurrencyAsync(4);
        Page(await harness.Controller().Index(CancellationToken.None))
            .EhrSystems.Should().ContainSingle().Which.MaxConcurrency.Should().Be(4);
    }

    [Fact]
    public async Task Starting_a_batch_repeats_each_scenario_and_keeps_the_lowered_cap()
    {
        var harness = new LaunchHarness();
        var scheduled = harness.AddScenario("scheduled", ReportMethod.ScheduledReport);
        var adhoc = harness.AddScenario("adhoc", ReportMethod.Adhoc);

        var result = await harness.Controller().Index(
            [Row("local", [scheduled.Id, adhoc.Id], copies: 2, maxConcurrency: 1)],
            CancellationToken.None);

        var batch = harness.Batches.Only();
        batch.LaunchId.Should().NotBeNull();
        result.Should().BeOfType<RedirectResult>().Which.Url
            .Should().Be("/Automation/ReportBatches/Launches/" + batch.LaunchId!.Value.ToString("D"));
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
        model.Errors.Should().Contain("other is not a configured EHR system.");
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
        model.Errors.Should().Contain($"local: Scenario {missing} was not found.");
    }

    [Fact]
    public async Task Too_many_copies_is_rejected()
    {
        var harness = new LaunchHarness();
        var scenario = harness.AddScenario("alpha");

        var model = await Rejected(harness, "local", [scenario.Id], 101, 1);
        model.Errors.Should().Contain("local: A batch can include at most 100 scenarios.");
        model.EhrSystems.Should().ContainSingle().Which.Copies.Should().Be(101);
    }

    [Fact]
    public async Task A_cap_above_the_saved_cap_is_rejected()
    {
        var harness = new LaunchHarness();
        var scenario = harness.AddScenario("alpha");

        var model = await Rejected(harness, "local", [scenario.Id], 1, 4);
        model.Errors.Should().Contain("local: maxConcurrency cannot be above the saved cap of 2.");
        (await harness.Settings.GetMaxConcurrencyAsync()).Should().Be(2);
    }

    [Fact]
    public async Task A_scenario_without_a_period_or_patients_is_rejected()
    {
        var harness = new LaunchHarness();
        var scenario = harness.AddScenario("alpha");
        scenario.ReportPeriodEnd = null;

        var startOnly = await Rejected(harness, "local", [scenario.Id], 1, 1);
        startOnly.Errors.Should().Contain("local: alpha needs both ends of the report period.");

        scenario.ReportPeriodStart = null;
        scenario.ReportPeriodEnd = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero);
        var endOnly = await Rejected(harness, "local", [scenario.Id], 1, 1);
        endOnly.Errors.Should().Contain("local: alpha needs both ends of the report period.");

        scenario.ReportPeriodStart = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);
        scenario.ReportPeriodEnd = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var reversed = await Rejected(harness, "local", [scenario.Id], 1, 1);
        reversed.Errors.Should().Contain("local: alpha has a report period that starts after it ends.");

        scenario.ReportPeriodEnd = new DateTimeOffset(2024, 2, 28, 0, 0, 0, TimeSpan.Zero);
        scenario.PatientCount = 0;
        var missingPatients = await Rejected(harness, "local", [scenario.Id], 1, 1);
        missingPatients.Errors.Should().Contain("local: alpha needs a patient source.");
    }

    [Fact]
    public async Task A_scenario_with_no_period_and_patients_is_accepted()
    {
        var harness = new LaunchHarness();
        var scenario = harness.AddScenario("seeded");
        scenario.ReportPeriodStart = null;
        scenario.ReportPeriodEnd = null;

        var result = await harness.Controller().Index(
            [Row("local", [scenario.Id])],
            CancellationToken.None);

        var batch = harness.Batches.Only();
        result.Should().BeOfType<RedirectResult>().Which.Url
            .Should().Be("/Automation/ReportBatches/Launches/" + batch.LaunchId!.Value.ToString("D"));
        batch.Items.Should().ContainSingle().Which.ScenarioId.Should().Be(scenario.Id);
    }

    [Fact]
    public async Task The_page_stays_closed_until_the_engine_is_ready()
    {
        var harness = new LaunchHarness(HttpStatusCode.OK, engineReady: false);

        var model = Page(await harness.Controller().Index(CancellationToken.None));
        model.Ready.Should().BeFalse();
        model.Errors.Should().Contain(ReportBatchLaunchController.Unavailable);
        model.EhrSystems.Should().ContainSingle().Which.Ready.Should().BeTrue();

        var posted = Page(await harness.Controller().Index(
            [Row("local", [Guid.NewGuid()])],
            CancellationToken.None));
        posted.Ready.Should().BeFalse();
        posted.Errors.Should().Contain(ReportBatchLaunchController.Unavailable);
    }

    [Fact]
    public void The_launch_page_is_on_the_nav_and_the_scenario_label_is_renamed()
    {
        var launch = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/ReportBatchLaunch/Index.cshtml"));
        launch.Should().Contain("name=\"ehrs[@index].Selected\"");
        launch.Should().Contain("name=\"ehrs[@index].ScenarioIds\"");
        launch.Should().Contain("name=\"ehrs[@index].Copies\"");
        launch.Should().Contain("name=\"ehrs[@index].MaxConcurrency\"");
        launch.Should().Contain("Start launch");
        launch.Should().Contain("field.disabled = !selected");
        launch.Should().Contain("Facility template");
        launch.Should().Contain("Edit scenarios");

        var launchPage = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/ReportBatchLaunchTracking/Detail.cshtml"));
        launchPage.Should().Contain("Cancel launch");
        launchPage.Should().Contain("Cancel this EHR");
        launchPage.Should().Contain("Pending");
        launchPage.Should().Contain("Failed");

        var script = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/automation-launch.js"));
        script.Should().Contain("2000");
        script.Should().NotContain("signalR");
        script.Should().NotContain("/api/report-batches");
        script.Should().Contain("if (response.status === 404)");

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

    [Fact]
    public async Task An_EHR_named_with_an_ampersand_launches_under_that_name()
    {
        var harness = new LaunchHarness(primaryName: "R&D");
        var scenario = harness.AddScenario("alpha");

        var rejected = Page(await harness.Controller().Index(
            [Row("R&D", [scenario.Id], maxConcurrency: 4)],
            CancellationToken.None));
        harness.Batches.Count.Should().Be(0);
        rejected.EhrSystems.Should().ContainSingle().Which.Selected.Should().BeTrue();
        rejected.Errors.Should().Contain("R&D: maxConcurrency cannot be above the saved cap of 2.");

        var result = await harness.Controller().Index(
            [Row("R&D", [scenario.Id])],
            CancellationToken.None);
        result.Should().BeOfType<RedirectResult>();
        harness.Batches.Only().EhrSystem.Should().Be("R&D");
    }

    [Fact]
    public async Task The_default_EHR_shows_its_facility_template()
    {
        var harness = new LaunchHarness(template: "Ward A");

        var ehr = Page(await harness.Controller().Index(CancellationToken.None))
            .EhrSystems.Should().ContainSingle().Subject;
        ehr.FacilityTemplate.Should().Be("Ward A");
    }

    [Fact]
    public async Task Two_EHRs_keep_their_own_scenarios_and_open_one_launch()
    {
        var harness = new LaunchHarness(secondEhr: true);
        var local = harness.AddScenario("local-scenario");
        var clinic = harness.AddScenario("clinic-scenario");

        var result = await harness.Controller().Index(
            [
                Row("local", [local.Id], copies: 2, maxConcurrency: 1),
                Row("clinic", [clinic.Id], copies: 1, maxConcurrency: 1)
            ],
            CancellationToken.None);

        var batches = harness.Batches.All();
        batches.Should().HaveCount(2);
        var launchId = batches.Select(batch => batch.LaunchId).Distinct().Should().ContainSingle().Subject;
        result.Should().BeOfType<RedirectResult>().Which.Url
            .Should().Be("/Automation/ReportBatches/Launches/" + launchId!.Value.ToString("D"));
        batches.Single(batch => batch.EhrSystem == "local").Items.Select(item => item.ScenarioId)
            .Should().Equal(local.Id, local.Id);
        batches.Single(batch => batch.EhrSystem == "clinic").Items.Should().ContainSingle()
            .Which.ScenarioId.Should().Be(clinic.Id);
        batches.Should().OnlyContain(batch => batch.MaxConcurrency == 1);
        harness.Runs.Started.Select(request => request.EhrSystem).Should().Equal("local", "clinic");

        var page = Summary(await harness.LaunchPage().Detail(launchId.Value, CancellationToken.None));
        page.Found.Should().BeTrue();
        page.Launch!.Groups.Select(group => group.EhrSystem).Should().Equal("clinic", "local");
        page.Launch.Pending.Should().Be(1);
        page.Launch.InProgress.Should().Be(2);
        page.Launch.Completed.Should().Be(0);
        page.Launch.Failed.Should().Be(0);
        page.Launch.Total.Should().Be(3);
        page.Launch.Groups.Should().OnlyContain(group => group.Items.All(item => item.EhrSystem == group.EhrSystem));
    }

    [Fact]
    public async Task A_not_ready_EHR_blocks_the_whole_launch()
    {
        var harness = new LaunchHarness(secondEhr: true, clinicStatus: HttpStatusCode.ServiceUnavailable);
        var scenario = harness.AddScenario("alpha");

        var model = Page(await harness.Controller().Index(
            [
                Row("local", [scenario.Id]),
                Row("clinic", [scenario.Id])
            ],
            CancellationToken.None));

        harness.Batches.Count.Should().Be(0);
        model.Errors.Should().Contain(error =>
            error.Contains("clinic", StringComparison.Ordinal)
            && error.Contains("not ready", StringComparison.Ordinal));
        model.Errors.Should().NotContain(error => error.StartsWith("local is not ready", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_unchecked_EHR_that_is_not_ready_does_not_block_the_launch()
    {
        var harness = new LaunchHarness(secondEhr: true, clinicStatus: HttpStatusCode.ServiceUnavailable);
        var scenario = harness.AddScenario("alpha");

        var result = await harness.Controller().Index(
            [
                Row("local", [scenario.Id]),
                Row("clinic", [scenario.Id], selected: false)
            ],
            CancellationToken.None);

        result.Should().BeOfType<RedirectResult>();
        harness.Batches.All().Should().ContainSingle().Which.EhrSystem.Should().Be("local");
    }

    [Fact]
    public async Task A_cap_above_the_saved_cap_on_one_EHR_creates_nothing()
    {
        var harness = new LaunchHarness(secondEhr: true);
        var scenario = harness.AddScenario("alpha");

        var model = Page(await harness.Controller().Index(
            [
                Row("local", [scenario.Id], maxConcurrency: 4),
                Row("clinic", [scenario.Id], maxConcurrency: 1)
            ],
            CancellationToken.None));

        harness.Batches.Count.Should().Be(0);
        model.Errors.Should().Contain(error =>
            error.Contains("local:", StringComparison.Ordinal)
            && error.Contains("saved cap of 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancelling_one_EHR_leaves_the_other_running()
    {
        var harness = new LaunchHarness(secondEhr: true);
        var local = harness.AddScenario("local-scenario");
        var clinic = harness.AddScenario("clinic-scenario");
        await harness.Controller().Index(
            [
                Row("local", [local.Id], copies: 2, maxConcurrency: 1),
                Row("clinic", [clinic.Id])
            ],
            CancellationToken.None);

        var launchId = harness.Batches.All().Select(batch => batch.LaunchId).Distinct().Single()!.Value;
        var localBatch = harness.Batches.All().Single(batch => batch.EhrSystem == "local");
        var page = harness.LaunchPage();

        var result = await page.CancelBatch(launchId, localBatch.BatchId, CancellationToken.None);
        result.Should().BeOfType<RedirectToActionResult>().Which.ActionName.Should().Be("Detail");

        var after = harness.Batches.All();
        after.Single(batch => batch.EhrSystem == "local").Status.Should().Be(ReportBatchStatus.Cancelled);
        after.Single(batch => batch.EhrSystem == "clinic").Status.Should().Be(ReportBatchStatus.Running);

        var missing = await page.CancelBatch(launchId, Guid.NewGuid(), CancellationToken.None);
        missing.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Cancelling_the_launch_cancels_every_open_EHR()
    {
        var harness = new LaunchHarness(secondEhr: true);
        var local = harness.AddScenario("local-scenario");
        var clinic = harness.AddScenario("clinic-scenario");
        await harness.Controller().Index(
            [
                Row("local", [local.Id]),
                Row("clinic", [clinic.Id])
            ],
            CancellationToken.None);

        var launchId = harness.Batches.All().Select(batch => batch.LaunchId).Distinct().Single()!.Value;
        var page = harness.LaunchPage();
        var result = await page.Cancel(launchId, CancellationToken.None);
        result.Should().BeOfType<RedirectToActionResult>().Which.ActionName.Should().Be("Detail");
        harness.Batches.All().Should().OnlyContain(batch => batch.Status == ReportBatchStatus.Cancelled);

        var status = await page.Status(launchId, CancellationToken.None);
        var body = status.Should().BeOfType<JsonResult>()
            .Which.Value.Should().BeOfType<ReportBatchLaunchSummaryPage>().Subject;
        body.Launch!.Cancelled.Should().Be(2);
        body.Launch.CanCancel.Should().BeFalse();

        (await page.Cancel(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NotFoundResult>();
        var missing = await page.Detail(Guid.NewGuid(), CancellationToken.None);
        Summary(missing).Found.Should().BeFalse();
        page.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
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
            [Row(ehrSystem, scenarioIds, copies, maxConcurrency)],
            CancellationToken.None));
        harness.Batches.Count.Should().Be(0);
        return model;
    }

    private static ReportBatchLaunchSummaryPage Summary(IActionResult result)
        => result.Should().BeOfType<ViewResult>().Which.Model.Should().BeOfType<ReportBatchLaunchSummaryPage>().Subject;

    private static ReportBatchLaunchEhrForm Row(
        string name,
        IEnumerable<Guid>? scenarioIds = null,
        int copies = 1,
        int maxConcurrency = 1,
        bool selected = true)
        => new()
        {
            Selected = selected,
            Name = name,
            ScenarioIds = scenarioIds?.ToList() ?? [],
            Copies = copies,
            MaxConcurrency = maxConcurrency
        };

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
        public LaunchHarness(
            HttpStatusCode probeStatus = HttpStatusCode.OK,
            bool engineReady = true,
            bool secondEhr = false,
            HttpStatusCode clinicStatus = HttpStatusCode.OK,
            string? template = null,
            string primaryName = "local")
        {
            Settings = new MemorySettings();
            Scenarios = new MemoryScenarios();
            Batches = new MemoryBatches();
            Runs = new FakeRuns();
            var values = new Dictionary<string, string?>
            {
                ["Automation:EhrSystems:0:Name"] = primaryName,
                ["Automation:EhrSystems:0:FhirServerBase"] = "http://127.0.0.1:9/fhir",
                ["Automation:EhrSystems:0:IsDefault"] = "true"
            };
            if (!string.IsNullOrWhiteSpace(template))
                values["Automation:EhrSystems:0:DefaultFacilityTemplate"] = template;
            if (secondEhr)
            {
                values["Automation:EhrSystems:1:Name"] = "clinic";
                values["Automation:EhrSystems:1:FhirServerBase"] = "http://127.0.0.1:9/clinic/fhir";
                values["Automation:EhrSystems:1:IsDefault"] = "false";
            }

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IOptionsMonitor<AutomationConfig>>(new FixedMonitor(new AutomationConfig()));
            services.AddSingleton<EhrSystemCatalog>();
            services.AddHttpClient(EhrSystemReadinessChecker.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new StatusHandler(probeStatus, clinicStatus));
            services.AddSingleton<EhrSystemReadinessChecker>();
            services.AddSingleton<IScenarioStore>(Scenarios);
            services.AddSingleton<IReportBatchSettingsStore>(Settings);
            services.AddSingleton<IReportBatchStore>(Batches);
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

        public ReportBatchLaunchTrackingController LaunchPage()
            => new(Services)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), new UnusedTempData())
            };

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

    private sealed class StatusHandler(HttpStatusCode local, HttpStatusCode clinic) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var status = path.Contains("/clinic/", StringComparison.Ordinal) ? clinic : local;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class FakeEhr : IReportBatchEhrLookup
    {
        public string? CanonicalName(string? requestedName)
        {
            var trimmed = requestedName?.Trim();
            if (string.Equals(trimmed, "local", StringComparison.OrdinalIgnoreCase))
                return "local";
            if (string.Equals(trimmed, "clinic", StringComparison.OrdinalIgnoreCase))
                return "clinic";
            if (string.Equals(trimmed, "R&D", StringComparison.Ordinal))
                return "R&D";
            return null;
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

        public IReadOnlyList<ReportBatchDocument> All() => _batches.Values.ToList();

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

        public Task<IReadOnlyList<ReportBatchDocument>> ListRecentAsync(int limit, CancellationToken cancellationToken = default)
        {
            if (limit < 1)
                return Task.FromResult<IReadOnlyList<ReportBatchDocument>>([]);

            IReadOnlyList<ReportBatchDocument> rows = _batches.Values
                .OrderByDescending(batch => batch.CreatedAt)
                .ThenBy(batch => batch.BatchId)
                .Take(limit)
                .ToList();
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<ReportBatchDocument>> ListByLaunchAsync(
            Guid launchId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReportBatchDocument>>(
                _batches.Values.Where(batch => batch.LaunchId == launchId).ToList());
    }

    private sealed class UnusedTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
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
