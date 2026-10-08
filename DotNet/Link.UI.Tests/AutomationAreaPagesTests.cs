using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using Link.UI.Controllers;
using Link.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Link.UI.Tests;

public class AutomationAreaPagesTests
{
    [Fact]
    public void New_run_form_stays_on_the_page_until_the_run_opens()
    {
        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Automation/New.cshtml"));
        view.Should().Contain("data-au-save=\"navigate\"");
        view.Should().Contain("id=\"startError\"");
        view.Should().NotContain("name=\"choice\" class=\"form-select\" required");

        var js = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/live-region.js"));
        js.Should().Contain("data-au-save\") === \"navigate\"");
        js.Should().Contain("window.location.assign(");
        js.Should().Contain("applyPage(html, res.url)");
    }

    [Fact]
    public void Section_nav_opens_metrics_api_health_and_cleanup()
    {
        var nav = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Automation/_Nav.cshtml"));
        nav.Should().Contain("asp-controller=\"Metrics\"");
        nav.Should().Contain("asp-controller=\"ApiHealth\"");
        nav.Should().Contain("asp-controller=\"Cleanup\"");
        nav.Should().NotContain("Later slice");

        var engine = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/LinkAutomationEngine.cs"));
        engine.Should().Contain("IRunMetricsSnapshotService, RunMetricsSnapshotService");
        engine.Should().Contain("IApiHealthRunStore, MongoApiHealthRunStore");
        engine.Should().Contain("AddHostedService<ApiHealthStartupRecoveryService>");
        engine.Should().Contain("AddHostedService<PatientBundleExternalizationMigrationService>()");
        engine.Should().NotContain("DashboardSeedService");
    }

    [Fact]
    public void Metrics_and_api_health_use_this_apps_run_pages()
    {
        var details = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Metrics/Details.cshtml"));
        details.Should().Contain("asp-controller=\"Automation\" asp-action=\"Run\"");
        details.Should().NotContain("asp-controller=\"Runs\"");

        var index = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Metrics/Index.cshtml"));
        index.Should().Contain("Url.Action(\"New\", \"Automation\")");
        index.Should().Contain("scenario:");
        index.Should().Contain("au-data-table.js");
        index.Should().NotContain("asp-controller=\"Runs\"");

        var scenario = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Metrics/Scenario.cshtml"));
        scenario.Should().Contain("au-data-table.js");

        var development = File.ReadAllText(RepoFile("DotNet/Link.UI/appsettings.Development.json"));
        development.Should().Contain("\"EnableAdminBffAuthSuite\": true");

        var health = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/ApiHealth/Index.cshtml"));
        health.Should().Contain("/Automation/Runs/");
        health.Should().Contain("/Automation/data?");
        health.Should().NotContain("/api/runs/");
        health.Should().NotContain("/Runs/DashboardStats");
        health.Should().NotContain("/Runs/Details/");
    }

    [Fact]
    public void Cleanup_page_keeps_the_automation_facility_limit()
    {
        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Cleanup/Index.cshtml"));
        view.Should().Contain("Named production facilities and other GUID tenants that were not created by Automation.UI are never selected.");
        view.Should().Contain("/hubs/cleanup");
        view.Should().Contain("SubscribeCleanup");
        view.Should().Contain("window.luPaintTimes(body)");
    }

    [Fact]
    public async Task Cleanup_save_without_a_run_does_not_start_a_pass()
    {
        var cleanup = new FakeCleanup();
        var store = new FakeSettings();
        var sut = Create(cleanup, store, new FakeReports());

        var result = await sut.SaveSettings(new CleanupSettingsForm { QuiesceIntervalMinutes = 100000 }, runKind: "", CancellationToken.None);

        result.Should().BeOfType<JsonResult>();
        store.Saved.Should().NotBeNull();
        store.Saved!.QuiesceInterval.Should().Be(TimeSpan.FromHours(24));
        store.Saved.Enabled.Should().BeFalse();
        cleanup.Starts.Should().BeEmpty();
    }

    [Fact]
    public async Task Cleanup_custom_range_rejects_an_empty_choice_and_an_inverted_range()
    {
        var cleanup = new FakeCleanup();
        var sut = Create(cleanup, new FakeSettings(), new FakeReports());

        var empty = await sut.RunCustomRange(new CleanupCustomRangeForm
        {
            FromDate = new DateTime(2026, 10, 1),
            ToDate = new DateTime(2026, 10, 2)
        }, CancellationToken.None);
        empty.Should().BeOfType<ConflictObjectResult>();

        var inverted = await sut.RunCustomRange(new CleanupCustomRangeForm
        {
            FromDate = new DateTime(2026, 10, 3),
            ToDate = new DateTime(2026, 10, 1),
            TeardownFacilities = true
        }, CancellationToken.None);
        inverted.Should().BeOfType<ConflictObjectResult>();
        cleanup.Starts.Should().BeEmpty();
    }

    [Fact]
    public async Task Cleanup_custom_range_starts_only_the_range_pass()
    {
        var cleanup = new FakeCleanup();
        var sut = Create(cleanup, new FakeSettings(), new FakeReports());

        var result = await sut.RunCustomRange(new CleanupCustomRangeForm
        {
            FromDate = new DateTime(2026, 10, 7),
            ToDate = new DateTime(2026, 10, 7),
            TeardownFacilities = true,
            PurgeHistory = false
        }, CancellationToken.None);

        result.Should().BeOfType<JsonResult>();
        cleanup.Starts.Should().Equal("custom-range");
        cleanup.LastFrom.Should().Be(new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero));
        cleanup.LastTo.Should().Be(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero));
        cleanup.LastTeardown.Should().BeTrue();
        cleanup.LastPurge.Should().BeFalse();
    }

    [Fact]
    public async Task Cleanup_unknown_run_kind_does_not_start()
    {
        var cleanup = new FakeCleanup();
        var sut = Create(cleanup, new FakeSettings(), new FakeReports());

        var result = await sut.SaveSettings(new CleanupSettingsForm(), "everything", CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        cleanup.Starts.Should().BeEmpty();
    }

    [Fact]
    public async Task Cleanup_report_rejects_an_empty_id()
    {
        var reports = new FakeReports();
        var sut = Create(new FakeCleanup(), new FakeSettings(), reports);

        var result = await sut.Report(Guid.Empty, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Invalid Id format");
        reports.Reads.Should().Be(0);
    }

    [Fact]
    public async Task Cleanup_does_not_start_when_the_engine_is_off()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new LinkAutomationEngineStatus { Ready = false, Message = "Engine off." });
        var provider = services.BuildServiceProvider();
        var sut = new CleanupController(provider, provider.GetRequiredService<LinkAutomationEngineStatus>(), provider.GetRequiredService<ILogger<CleanupController>>());
        sut.ControllerContext = JsonContext();

        var result = await sut.RunCustomRange(new CleanupCustomRangeForm
        {
            FromDate = new DateTime(2026, 10, 7),
            ToDate = new DateTime(2026, 10, 7),
            TeardownFacilities = true
        }, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    private static CleanupController Create(FakeCleanup cleanup, FakeSettings settings, FakeReports reports)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new LinkAutomationEngineStatus { Ready = true });
        services.AddSingleton<ILeftoverRunCleanup>(cleanup);
        services.AddSingleton<ICleanupSettingsStore>(settings);
        services.AddSingleton<ICleanupReportStore>(reports);
        var provider = services.BuildServiceProvider();
        var sut = new CleanupController(
            provider,
            provider.GetRequiredService<LinkAutomationEngineStatus>(),
            provider.GetRequiredService<ILogger<CleanupController>>());
        sut.ControllerContext = JsonContext();
        return sut;
    }

    private static ControllerContext JsonContext()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        return new ControllerContext { HttpContext = http };
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;

        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class FakeCleanup : ILeftoverRunCleanup
    {
        public List<string> Starts { get; } = [];

        public DateTimeOffset? LastFrom { get; private set; }

        public DateTimeOffset? LastTo { get; private set; }

        public bool LastTeardown { get; private set; }

        public bool LastPurge { get; private set; }

        public DateTimeOffset? LastQuiesceAt => null;

        public LeftoverCleanupResult? LastQuiesceResult => null;

        public CleanupActivity CurrentActivity => CleanupActivity.Idle;

        public bool IsRunning => false;

        public void StartQuiesceInBackground() => Starts.Add("quiesce");

        public void StartTeardownInBackground() => Starts.Add("teardown");

        public void StartHistoryPurgeInBackground() => Starts.Add("history-purge");

        public void StartCustomRangeInBackground(
            DateTimeOffset fromInclusiveUtc,
            DateTimeOffset toExclusiveUtc,
            bool teardownFacilities,
            bool purgeHistory)
        {
            Starts.Add("custom-range");
            LastFrom = fromInclusiveUtc;
            LastTo = toExclusiveUtc;
            LastTeardown = teardownFacilities;
            LastPurge = purgeHistory;
        }
    }

    private sealed class FakeSettings : ICleanupSettingsStore
    {
        public LeftoverRunCleanupSettings? Saved { get; private set; }

        public Task<LeftoverRunCleanupSettings> GetEffectiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new LeftoverRunCleanupSettings());

        public Task SaveAsync(LeftoverRunCleanupSettings settings, CancellationToken cancellationToken = default)
        {
            Saved = settings;
            return Task.CompletedTask;
        }

        public Task RecordDailyTeardownAsync(DateTimeOffset at, string result, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecordWeeklyPurgeAsync(DateTimeOffset at, string result, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeReports : ICleanupReportStore
    {
        public int Reads { get; private set; }

        public Task SaveAsync(CleanupReport report, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<CleanupReport>> ListRecentAsync(int limit = 25, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CleanupReport>>([]);

        public Task<CleanupReport?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult<CleanupReport?>(null);
        }
    }
}
