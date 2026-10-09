using Automation.UI.Models.ApiHealth;
using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Enums;
using Link.UI.Controllers;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Link.UI.Tests;

public class HomeOverviewTests
{
    [Fact]
    public void Facility_split_uses_the_ownership_index()
    {
        var index = new AutomationOwnershipIndex(
            ["owned-1"],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["owned-1"] = "run-1" });

        var card = HomeOverviewRules.Facilities(
            true,
            null,
            true,
            ["owned-1", "regular-1", "owned-1", " "],
            index);

        card.Reachable.Should().BeTrue();
        card.Total.Should().Be(2);
        card.Automation.Should().Be(1);
        card.Regular.Should().Be(1);
        card.Message.Should().BeNull();
    }

    [Fact]
    public void Unreachable_tenant_list_does_not_look_like_zero_facilities()
    {
        var card = HomeOverviewRules.Facilities(false, "Tenant service could not be reached.", true, ["kept"], null);

        card.Reachable.Should().BeFalse();
        card.TotalText.Should().Be("—");
        card.Message.Should().Be("Tenant service could not be reached.");
    }

    [Fact]
    public void Ownership_miss_keeps_the_facility_total_and_hides_the_split()
    {
        var card = HomeOverviewRules.Facilities(true, null, false, ["a", "b"], AutomationOwnershipIndex.Empty);

        card.Reachable.Should().BeTrue();
        card.Total.Should().Be(2);
        card.AutomationText.Should().Be("—");
        card.RegularText.Should().Be("—");
        card.Message.Should().Be("Automation ownership could not be read.");
    }

    [Fact]
    public void Empty_facility_list_is_a_real_zero()
    {
        HomeOverviewRules.TryReadFacilities(204, true, null, out var ids).Should().BeTrue();
        ids.Should().BeEmpty();

        var card = HomeOverviewRules.Facilities(true, null, true, ids, AutomationOwnershipIndex.Empty);
        card.Reachable.Should().BeTrue();
        card.Total.Should().Be(0);
        card.Automation.Should().Be(0);
    }

    [Fact]
    public void Reports_keep_five_and_an_unreachable_list_stays_empty()
    {
        var rows = Enumerable.Range(0, 8).Select(i => new FacilityReportRow
        {
            Id = Guid.NewGuid(),
            FacilityId = "facility-" + i,
            Status = ScheduleStatus.Submitted,
            StatusLabel = "Submitted",
            Created = "1/2/26 03:04"
        });

        var card = HomeOverviewRules.Reports(true, null, 80, rows);
        card.Rows.Should().HaveCount(5);
        card.Total.Should().Be(80);
        card.Rows[0].Badge.Should().Be("au-badge-success");

        var down = HomeOverviewRules.Reports(false, "Report service could not be reached.", 80, rows);
        down.Reachable.Should().BeFalse();
        down.Rows.Should().BeEmpty();
        down.TotalText.Should().Be("—");
    }

    [Fact]
    public void Health_counts_unhealthy_services_and_leaves_api_health_on_its_own()
    {
        var card = HomeOverviewRules.Health(
            true,
            null,
            [
                ("account", "Healthy"),
                ("audit", "Unhealthy"),
                ("report", "ok"),
                ("census", "Degraded"),
                ("normalization", "Unhealthy"),
                ("measureeval", "Failed"),
                ("validation", "Unhealthy")
            ],
            apiReachable: false,
            apiMessage: "API health storage could not be read.",
            apiRunId: null,
            apiMode: null,
            apiService: null,
            apiWhen: null);

        card.Healthy.Should().Be(2);
        card.Unhealthy.Should().Be(5);
        card.UnhealthyNames.Should().Equal("audit", "census", "normalization", "measureeval");
        card.HiddenUnhealthy.Should().Be(1);
        card.ApiHealthReachable.Should().BeFalse();
        card.ApiHealthText.Should().Be("API health storage could not be read.");
        card.UnhealthyText.Should().Be("5");
    }

    [Fact]
    public void Missing_api_health_run_is_empty_not_an_error()
    {
        HomeOverviewRules.ApiHealthText(true, null, null, null, null, null)
            .Should().Be("No API health run yet.");
    }

    [Fact]
    public void Latest_api_health_includes_a_scenario_run()
    {
        var morning = new DateTimeOffset(2026, 10, 7, 14, 29, 0, TimeSpan.Zero);
        var evening = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero).AddHours(20);
        var stored = new ApiHealthLatestRunContext
        {
            RunId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RunMode = "All",
            ServiceName = "",
            StartedAt = morning
        };
        var scenario = new AutomationRunRow
        {
            RunId = Guid.Parse("a5bf9393-61fc-4f4c-85cc-c6eb817551ec"),
            RunName = HomeOverviewRules.ApiHealthScenarioName,
            Scenario = "Custom",
            Status = "Succeeded",
            CreatedAt = evening.AddMinutes(-1),
            StartedAt = evening.AddMinutes(-1),
            FinishedAt = evening
        };

        var chosen = HomeOverviewRules.ChooseLatestApiHealth(stored, execution: null, scenario);
        chosen.Should().NotBeNull();
        chosen!.RunId.Should().Be(scenario.RunId);
        chosen.At.Should().Be(evening);
        chosen.Mode.Should().Be("All");

        var execution = new ApiHealthExecutionRunStatus
        {
            RunId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Scope = "All",
            StartedAt = morning,
            FinishedAt = evening.AddMinutes(5),
            IsCompleted = true,
            SeedRunId = scenario.RunId
        };
        var finished = HomeOverviewRules.ChooseLatestApiHealth(stored, execution, scenario);
        finished!.RunId.Should().Be(execution.RunId);
        finished.At.Should().Be(execution.FinishedAt!.Value);

        HomeOverviewRules.ChooseLatestApiHealth(null, null, null).Should().BeNull();
        HomeOverviewRules.When(evening).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Runs_link_by_id_and_stop_at_five()
    {
        var rows = Enumerable.Range(0, 6).Select(i => new AutomationRunRow
        {
            RunId = Guid.Parse($"00000000-0000-0000-0000-00000000000{i}"),
            RunName = "Run " + i,
            Status = i == 0 ? "Running" : "Succeeded",
            CreatedAt = DateTimeOffset.Parse("2026-10-07T12:00:00Z")
        }).ToList();

        var card = HomeOverviewRules.Runs(true, null, 6, rows, rows);
        card.ActiveCount.Should().Be(6);
        card.Active.Should().HaveCount(5);
        card.Active[0].Badge.Should().Be("au-badge-active");
        card.Active[0].Id.Should().Be("00000000-0000-0000-0000-000000000000");
        card.Recent.Should().ContainSingle();
        card.Recent[0].Id.Should().Be("00000000-0000-0000-0000-000000000005");
        card.Recent[0].Badge.Should().Be("au-badge-success");

        var down = HomeOverviewRules.Runs(false, AutomationRunReader.UnreachableMessage, 6, rows, rows);
        down.Reachable.Should().BeFalse();
        down.Active.Should().BeEmpty();
        down.ActiveText.Should().Be("—");
    }

    [Fact]
    public void Audit_failure_does_not_drop_acquisition_rows()
    {
        var card = HomeOverviewRules.Logs(
            true,
            null,
            3,
            [
                new HomeLogLine { Id = 9, FacilityId = "fac", Status = "Failed", Badge = "au-badge-danger", When = "now" }
            ],
            false,
            "Audit could not be reached.");

        card.Reachable.Should().BeTrue();
        card.Rows.Should().ContainSingle();
        card.Total.Should().Be(3);
        card.AuditMessage.Should().Be("Audit could not be reached.");
    }

    [Fact]
    public void One_unreachable_card_keeps_the_others()
    {
        var facilities = HomeOverviewRules.Facilities(
            true,
            null,
            true,
            ["only"],
            AutomationOwnershipIndex.Empty);
        var health = HomeOverviewRules.Health(
            false,
            "Admin.BFF could not be reached.",
            [("account", "Healthy")],
            true,
            null,
            null,
            null,
            null,
            null);

        facilities.Reachable.Should().BeTrue();
        facilities.Total.Should().Be(1);
        health.Reachable.Should().BeFalse();
        health.UnhealthyText.Should().Be("—");
        health.Healthy.Should().Be(0);
        health.ApiHealthText.Should().Be("No API health run yet.");
    }

    [Fact]
    public async Task A_failing_card_returns_its_empty_state()
    {
        var card = await HomeOverviewRules.LoadCardAsync<string>(
            _ => throw new InvalidOperationException("boom"),
            failure => failure == HomeOverviewRules.CardFailure.Timeout ? "timeout" : "error",
            CancellationToken.None,
            TimeSpan.FromSeconds(2));

        card.Should().Be("error");
    }

    [Fact]
    public async Task A_slow_card_times_out_without_throwing()
    {
        var card = await HomeOverviewRules.LoadCardAsync(
            async token =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                return "late";
            },
            failure => failure.ToString(),
            CancellationToken.None,
            TimeSpan.FromMilliseconds(50));

        card.Should().Be(nameof(HomeOverviewRules.CardFailure.Timeout));
    }

    [Fact]
    public async Task Caller_cancel_still_cancels_the_card()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => HomeOverviewRules.LoadCardAsync(
            async token =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                return "late";
            },
            _ => "failed",
            cts.Token,
            TimeSpan.FromSeconds(5));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Overview_json_returns_the_loaded_model()
    {
        var model = new HomeOverviewModel
        {
            Facilities = new FacilityCard { Reachable = true, Total = 4, Automation = 1, Regular = 3 },
            LoadedAt = DateTimeOffset.Parse("2026-10-07T15:00:00Z")
        };
        var controller = new HomeController(new FakeOverview(model));

        var result = await controller.OverviewData(CancellationToken.None);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.Value.Should().BeSameAs(model);
        var facilities = json.Value.Should().BeOfType<HomeOverviewModel>().Subject.Facilities;
        facilities.Total.Should().Be(4);
        facilities.Automation.Should().Be(1);
    }

    [Fact]
    public async Task Overview_fragment_uses_the_same_model()
    {
        var model = new HomeOverviewModel();
        var controller = new HomeController(new FakeOverview(model));

        var result = await controller.Overview(CancellationToken.None);

        var view = result.Should().BeOfType<PartialViewResult>().Subject;
        view.ViewName.Should().Be("_Overview");
        view.Model.Should().BeSameAs(model);
    }

    [Fact]
    public void Home_shell_loads_counts_after_paint_from_the_overview()
    {
        var index = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Home/Index.cshtml"));
        index.Should().Contain("data-au-refresh=\"15000\"");
        index.Should().Contain("data-au-refresh-url=\"@Url.Action(\"Overview\", \"Home\")\"");
        index.Should().Contain("id=\"homeOverview\"");
        index.Should().Contain("ViewData[\"Title\"] = \"Dashboard\"");
        index.Should().Contain(">Dashboard</h1>");
        index.Should().NotContain(">Home</h1>");
        index.Should().NotContain("Welcome");

        var layout = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_Layout.cshtml"));
        layout.Should().Contain("bi-house-door me-2\"></i>Dashboard");
        layout.Should().NotContain(">Home<");
        layout.Should().Contain("asp-controller=\"Home\"");

        var controller = File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/HomeController.cs"));
        controller.Should().Contain("class HomeController");
        controller.Should().Contain("[HttpGet(\"/dashboard\")]");
        controller.Should().Contain("ViewData[\"Title\"] = \"Dashboard\"");

        var js = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/live-region.js"));
        js.Should().Contain("data-au-refresh-url");
        js.Should().Contain("setTimeout(function () { refresh(node); }, 0);");
        js.Should().Contain("regionUrl(node)");

        index.Should().NotContain("automation");
        index.Should().Contain("lu-live-label");

        var overview = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Home/_Overview.cshtml"));
        overview.Should().Contain("asp-controller=\"Tenants\"");
        overview.Should().Contain("asp-route-status=\"New,Scheduled,EndOfPeriod\"");
        overview.Should().Contain("asp-route-status=\"Submitted\"");
        overview.Should().Contain("asp-route-status=\"CompletedNotSubmitted\"");
        overview.Should().Contain("asp-route-created=\"@day.Day\"");
        overview.Should().Contain("asp-controller=\"Reports\"");
        overview.Should().Contain("asp-controller=\"System\" asp-action=\"Health\"");
        overview.Should().Contain("asp-controller=\"ApiHealth\"");
        overview.Should().Contain("asp-controller=\"Metrics\"");
        overview.Should().Contain("asp-controller=\"Logs\" asp-action=\"Acquisition\"");
        overview.Should().Contain("asp-route-status=\"Failed,MaxRetriesReached\"");
        overview.Should().Contain("au-kpi-card");
        overview.Should().Contain("name=\"_ServiceMonitor\"");
        overview.Should().Contain("class=\"btn btn-sm btn-au-link\" asp-controller=\"System\" asp-action=\"Health\"");
        overview.Should().Contain("id=\"homeOverview\"");
        overview.Should().Contain("Nothing needs attention right now.");
        overview.Should().Contain("home-row-main");
        overview.Should().Contain("home-row-time text-muted small");
        var css = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/css/site.css"));
        css.Should().Contain("grid-template-columns: auto minmax(0, 1fr) auto;");
        css.Should().Contain(".home-row-time { grid-column: 1 / -1; justify-self: start; }");

        var gate = overview.IndexOf("@if (Model.AutomationVisible)", StringComparison.Ordinal);
        var scope = overview.IndexOf("asp-route-scope=\"automation\"", StringComparison.Ordinal);
        var run = overview.IndexOf("asp-controller=\"Automation\" asp-action=\"Run\"", StringComparison.Ordinal);
        gate.Should().BeGreaterThan(0);
        scope.Should().BeGreaterThan(gate);
        run.Should().BeGreaterThan(gate);
        overview[..gate].Should().NotContain("automation");

        var service = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/HomeOverviewService.cs"));
        service.Should().Contain("_features.Value.AutomationEnabled");
        service.Should().Contain("CacheKey + \":off\"");
        service.Should().Contain("AutomationOwnershipLookup");
        var options = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/LinkUiFeatureOptions.cs"));
        options.Should().Contain("bool AutomationEnabled");
    }

    [Fact]
    public void Thousands_of_facilities_and_reports_stay_counts()
    {
        var ids = Enumerable.Range(0, 4000).Select(i => "facility-" + i).ToArray();
        var owned = ids.Take(250).ToArray();
        var index = new AutomationOwnershipIndex(
            owned,
            owned.ToDictionary(id => id, _ => "run", StringComparer.OrdinalIgnoreCase));

        var card = HomeOverviewRules.Facilities(true, null, true, ids, index);
        card.Total.Should().Be(4000);
        card.Automation.Should().Be(250);
        card.Regular.Should().Be(3750);
        card.GetType().GetProperties().Should().NotContain(property =>
            property.PropertyType != typeof(string)
            && typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType));

        var rows = Enumerable.Range(0, 4000).Select(i => new FacilityReportRow
        {
            Id = Guid.NewGuid(),
            FacilityId = "facility-" + i,
            Status = ScheduleStatus.New,
            StatusLabel = "New",
            Created = "2026-10-01T00:00:00Z"
        });
        var reports = HomeOverviewRules.Reports(true, null, 9000, rows);
        reports.Rows.Should().HaveCount(HomeOverviewRules.RowLimit);
        reports.Total.Should().Be(9000);
        reports.Rows.Should().OnlyContain(row => row.FacilityId.StartsWith("facility-", StringComparison.Ordinal));

        var counted = HomeOverviewRules.FacilitiesFromCounts(true, null, 4000, 250, true, true);
        counted.Total.Should().Be(4000);
        counted.Automation.Should().Be(250);
        counted.Regular.Should().Be(3750);
        counted.GetType().GetProperties().Should().NotContain(property =>
            property.PropertyType != typeof(string)
            && typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType));

        var batches = HomeOverviewRules.FacilityIdBatches(ids);
        batches.Should().HaveCount(1);
        batches[0].Should().HaveCount(4000);
        var overflow = HomeOverviewRules.FacilityIdBatches(Enumerable.Range(0, 5001).Select(i => "id-" + i).ToArray());
        overflow.Should().HaveCount(2);
        overflow.Sum(batch => batch.Count).Should().Be(5001);

        var service = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/HomeOverviewService.cs"));
        var overview = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Home/_Overview.cshtml"));
        service.Should().NotContain("GetFacilityListAsync");
        service.Should().NotContain("CountReportsAsync");
        service.Should().Contain("GetFacilityCountsAsync");
        service.Should().Contain("RealFacilityScope");
        service.Should().Contain("LoadActivityCountsAsync");
        service.Should().Contain("LoadAcquisitionCountsAsync");
        service.Should().Contain("LoadAuditErrorsAsync");
        overview.Should().Contain("Acquisition throughput");
        overview.Should().Contain("Audit errors");
        overview.Should().Contain("UTC days");
    }

    [Fact]
    public void Hidden_automation_keeps_the_total_and_drops_the_split()
    {
        var known = HomeOverviewRules.Facilities(
            true,
            null,
            true,
            ["owned", "real-a", "real-b"],
            new AutomationOwnershipIndex(
                ["owned"],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["owned"] = "run" }));
        known.Regular.Should().Be(2);
        HomeOverviewRules.PrimaryFacilityText(known, true).Should().Be("2");
        HomeOverviewRules.PrimaryFacilityLabel(known, true).Should().Be("Real facilities");
        HomeOverviewRules.RealFacilityScope(true, known.Regular).Should().Be(AutomationMarkRules.Real);
        HomeOverviewRules.RealFacilityScope(true, null).Should().BeNull();
        HomeOverviewRules.RealFacilityScope(false, 2).Should().BeNull();

        var hidden = HomeOverviewRules.Facilities(
            true,
            null,
            false,
            ["owned", "real-a", "real-b"],
            AutomationOwnershipIndex.Empty,
            classify: false);
        hidden.Automation.Should().BeNull();
        hidden.Regular.Should().BeNull();
        hidden.Total.Should().Be(3);
        hidden.Message.Should().BeNull();
        HomeOverviewRules.PrimaryFacilityText(hidden, false).Should().Be("3");
        HomeOverviewRules.PrimaryFacilityLabel(hidden, false).Should().Be("Facilities");

        var now = DateTimeOffset.Parse("2026-10-07T15:00:00Z");
        var quietActivity = HomeOverviewRules.Activity(true, 1, now.AddHours(-1), true, 0, true, 0, []);
        var quietHealth = HomeOverviewRules.Health(true, null, [("account", "Healthy")], true, null, null, null, null, null);
        var quietLogs = HomeOverviewRules.Logs(true, null, 0, null, true, null);
        HomeOverviewRules.Issues(
            true,
            false,
            false,
            quietActivity,
            quietHealth,
            quietLogs,
            HomeOverviewRules.Pulse(false, "Service metrics could not be read.", null),
            now).Should().BeEmpty();
        HomeOverviewRules.Issues(
            true,
            false,
            true,
            quietActivity,
            quietHealth,
            quietLogs,
            HomeOverviewRules.Pulse(true, null, []),
            now).Should().ContainSingle(issue =>
                issue.Title == "Automation ownership could not be read"
                && issue.Href == HomeOverviewRules.AutomationTenantsHref);
    }

    [Fact]
    public void Issues_name_the_problem_and_stay_a_short_list()
    {
        var now = DateTimeOffset.Parse("2026-10-07T15:00:00Z");
        var activity = HomeOverviewRules.Activity(
            true,
            40,
            now.AddHours(-30),
            false,
            0,
            true,
            3,
            Enumerable.Range(0, 7).Select(i => new TrendDay
            {
                Day = "2026-10-0" + (i + 1),
                Reachable = false,
                Count = 9000
            }).ToList());
        var health = HomeOverviewRules.Health(
            true,
            null,
            Enumerable.Range(0, 4000).Select(i => ("service-" + i, "Unhealthy")),
            true,
            null,
            Guid.NewGuid(),
            "All",
            null,
            "2026-10-07T14:00:00Z");
        var logs = HomeOverviewRules.Logs(true, null, 4000, null, false, "Audit could not be reached.");
        var pulse = HomeOverviewRules.Pulse(true, null,
        [
            new PulseSample("platform-db", "platform", 90, 9000),
            new PulseSample("Report", "pipeline", 10, 1500),
            new PulseSample("Submission", "pipeline", 12, 3400),
            new PulseSample("Normalization", "pipeline", 8, 2600)
        ]);

        pulse.Chips.Should().HaveCount(3);
        pulse.Chips.Should().NotContain(chip => chip.Name == "platform-db");

        var issues = HomeOverviewRules.Issues(false, false, true, activity, health, logs, pulse, now);
        issues.Should().HaveCount(HomeOverviewRules.IssueLimit);
        issues.Select(issue => issue.Title).Should().Equal(
            "Facilities could not be read",
            "Report counts could not be read",
            "A report has been in flight for more than a day",
            "Report trend could not be read",
            "4000 unhealthy services",
            "4000 failed acquisition logs",
            "Audit could not be read",
            "Submission API is slower than 2 seconds");
        issues.Should().OnlyContain(issue => !issue.Title.Contains("facility-", StringComparison.Ordinal));
        issues[2].Href.Should().Be(HomeOverviewRules.InFlightHref);
        issues[2].Detail.Should().Be("2026-10-06 09:00:00 UTC");
        issues[4].Href.Should().Be(HomeOverviewRules.HealthHref);
        issues[4].Detail.Should().NotContain("service-4");
        issues[5].Href.Should().Be(HomeOverviewRules.FailedLogsHref);
        issues[7].Href.Should().Be(HomeOverviewRules.MetricsHref);
        issues[7].Detail.Should().Be("3400 ms");

        var quiet = HomeOverviewRules.Issues(
            true,
            true,
            false,
            HomeOverviewRules.Activity(true, 1, now.AddHours(-2), true, 4, true, 0, [new TrendDay { Day = "2026-10-07", Reachable = true, Count = 9000 }]),
            HomeOverviewRules.Health(true, null, [("account", "Healthy")], true, null, null, null, null, null),
            HomeOverviewRules.Logs(true, null, 0, null, true, null),
            HomeOverviewRules.Pulse(true, null, [new PulseSample("Report", "pipeline", 1, 20)]),
            now);
        quiet.Should().BeEmpty();

        var fresh = HomeOverviewRules.Activity(true, 1, now.AddHours(-1), true, 0, true, 0, [new TrendDay { Day = "2026-10-07", Reachable = true, Count = 0 }]);
        HomeOverviewRules.Issues(
            true,
            true,
            true,
            fresh,
            HomeOverviewRules.Health(true, null, [("account", "ok")], true, null, null, null, null, null),
            HomeOverviewRules.Logs(true, null, 0, null, true, null),
            HomeOverviewRules.Pulse(false, "Service metrics could not be read.", null),
            now).Should().ContainSingle(issue => issue.Href == HomeOverviewRules.MetricsHref && issue.Title == "Service metrics could not be read");

        var auditErrors = HomeOverviewRules.Logs(
            true, null, 0, null, true, null, true,
            [new TrendDay { Day = "2026-10-07", Reachable = true, Count = 4, Failed = 1 }],
            true, 3);
        HomeOverviewRules.Issues(
            true, true, false,
            HomeOverviewRules.Activity(true, 0, null, true, 0, true, 0, []),
            HomeOverviewRules.Health(true, null, [("account", "Healthy")], true, null, null, null, null, null),
            auditErrors,
            HomeOverviewRules.Pulse(true, null, []),
            now).Should().ContainSingle(issue => issue.Title == "3 audit errors in the last 24 hours" && issue.Href == HomeOverviewRules.AuditHref);

        var unread = HomeOverviewRules.Logs(false, "Data acquisition returned HTTP 404.", 0, null, false, "Audit returned HTTP 404.");
        unread.Reachable.Should().BeFalse();
        unread.TotalText.Should().Be("—");
        unread.TrendReachable.Should().BeFalse();
        unread.AuditCounted.Should().BeFalse();
        unread.AuditErrorsText.Should().Be("—");
        unread.Total.Should().Be(0);

        HomeOverviewRules.CreatedHref("2026-10-07").Should().Be("/Reports?created=2026-10-07");
        HomeOverviewRules.BarPercent(9000, 9000).Should().Be(100);
        HomeOverviewRules.BarPercent(0, 9000).Should().Be(0);
        fresh.InFlightText.Should().Be("1");
        HomeOverviewRules.Activity(false, 80, now, true, 1, true, 2, []).InFlightText.Should().Be("—");
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "link-cloud.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class FakeOverview : IHomeOverview
    {
        private readonly HomeOverviewModel _model;

        public FakeOverview(HomeOverviewModel model) => _model = model;

        public Task<HomeOverviewModel> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_model);
    }
}
