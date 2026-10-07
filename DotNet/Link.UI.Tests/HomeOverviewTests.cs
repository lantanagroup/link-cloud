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

        var overview = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Home/_Overview.cshtml"));
        overview.Should().Contain("asp-controller=\"Tenants\"");
        overview.Should().Contain("asp-route-scope=\"automation\"");
        overview.Should().Contain("asp-controller=\"Reports\"");
        overview.Should().Contain("asp-controller=\"System\" asp-action=\"Health\"");
        overview.Should().Contain("asp-controller=\"ApiHealth\"");
        overview.Should().Contain("asp-controller=\"Automation\" asp-action=\"Run\"");
        overview.Should().Contain("asp-controller=\"Logs\" asp-action=\"AcquisitionDetail\"");
        overview.Should().Contain("au-kpi-card");
        overview.Should().Contain("id=\"homeOverview\"");
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
