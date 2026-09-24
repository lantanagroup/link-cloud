using FluentAssertions;
using LantanaGroup.Automation;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class RunCleanupHelperTests
{
    private static readonly TimeSpan QuiesceGrace = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TeardownRetention = TimeSpan.FromDays(14);

    [Fact]
    public void Guid_facility_with_no_automation_run_is_not_selected()
    {
        var facilityId = Guid.NewGuid().ToString();
        var facilities = Facilities(facilityId);

        SelectQuiesce(facilities, []).Should().BeEmpty();
        SelectTeardown(facilities, []).Should().BeEmpty();
    }

    [Fact]
    public void Qa_guid_tenant_without_an_automation_run_is_not_selected()
    {
        var qaFacility = "bf47f291-f206-4716-bd89-41cc1fa649e7";
        SelectQuiesce(Facilities(qaFacility), []).Should().BeEmpty();
        SelectTeardown(Facilities(qaFacility), []).Should().BeEmpty();
    }

    [Fact]
    public void Named_facility_is_never_treated_as_automation_leftover()
    {
        var leftovers = SelectQuiesce(
            Facilities("echs", "demo-hospital"),
            runs: []);

        leftovers.Should().BeEmpty();
    }

    [Theory]
    [InlineData(AutomationRunStatus.Queued)]
    [InlineData(AutomationRunStatus.Running)]
    [InlineData(AutomationRunStatus.LiveWindowOpen)]
    [InlineData(AutomationRunStatus.ReportFinalization)]
    public void Active_run_facility_is_protected(AutomationRunStatus status)
    {
        var runId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();

        var leftovers = SelectQuiesce(
            Facilities(runId.ToString(), facilityId, Guid.NewGuid().ToString()),
            [Run(runId, facilityId, status, finishedAt: null)],
            now: DateTimeOffset.Parse("2026-08-28T20:00:00Z"));

        leftovers.Should().BeEmpty();
    }

    [Fact]
    public void Recently_finished_run_is_not_quiesced_within_grace()
    {
        var runId = Guid.NewGuid();
        var facilityId = runId.ToString();
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z");

        SelectQuiesce(
            Facilities(facilityId),
            [Run(runId, facilityId, AutomationRunStatus.Succeeded, finishedAt: now.AddMinutes(-1))],
            now).Should().BeEmpty();
    }

    [Fact]
    public void Finished_run_after_grace_is_quiesced_but_not_torn_down()
    {
        var runId = Guid.NewGuid();
        var facilityId = runId.ToString();
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z");
        var runs = new[] { Run(runId, facilityId, AutomationRunStatus.Failed, finishedAt: now.AddHours(-2)) };

        SelectQuiesce(Facilities(facilityId), runs, now).Should().Equal(facilityId);
        SelectTeardown(Facilities(facilityId), runs, now).Should().BeEmpty();
    }

    [Fact]
    public void Finished_run_after_teardown_retention_is_torn_down()
    {
        var runId = Guid.NewGuid();
        var facilityId = runId.ToString();
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z");

        SelectTeardown(
            Facilities(facilityId),
            [Run(runId, facilityId, AutomationRunStatus.Cancelled, finishedAt: now.AddDays(-15))],
            now).Should().Equal(facilityId);
    }

    [Fact]
    public void Stale_active_guid_run_is_selected_after_retention()
    {
        var runId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z");
        var stale = Run(runId, facilityId, AutomationRunStatus.Running, finishedAt: null);
        stale.AutomationCreatedFacility = true;
        stale.CreatedAt = now.AddDays(-15);
        stale.StartedAt = now.AddDays(-15);
        var runs = new[] { stale };

        RunCleanupHelper.SelectStaleActiveAutomationFacilities(
            Facilities(facilityId),
            runs,
            now,
            TeardownRetention).Should().Equal(facilityId);
    }

    [Fact]
    public void Stale_active_reused_guid_tenant_is_not_selected()
    {
        var runId = Guid.NewGuid();
        var tenantId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z");
        var stale = Run(runId, tenantId, AutomationRunStatus.Running, finishedAt: null);
        stale.CreatedAt = now.AddDays(-15);
        stale.StartedAt = now.AddDays(-15);

        RunCleanupHelper.SelectStaleActiveAutomationFacilities(
            Facilities(tenantId),
            [stale],
            now,
            TeardownRetention).Should().BeEmpty();
    }

    [Fact]
    public void Stale_creator_does_not_select_a_facility_a_newer_run_still_uses()
    {
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z");
        var shared = Guid.NewGuid().ToString();
        var stale = Run(Guid.NewGuid(), shared, AutomationRunStatus.Running, finishedAt: null);
        stale.AutomationCreatedFacility = true;
        stale.CreatedAt = now.AddDays(-15);
        stale.StartedAt = now.AddDays(-15);
        var newer = Run(Guid.NewGuid(), shared, AutomationRunStatus.Running, finishedAt: null);
        newer.CreatedAt = now.AddHours(-1);
        newer.StartedAt = now.AddHours(-1);

        RunCleanupHelper.SelectStaleActiveAutomationFacilities(
            Facilities(shared),
            [stale, newer],
            now,
            TeardownRetention).Should().BeEmpty();
    }

    [Fact]
    public void History_purge_selects_terminal_runs_past_retention()
    {
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z");
        var oldId = Guid.NewGuid();
        var recentId = Guid.NewGuid();
        var runs = new[]
        {
            Run(oldId, oldId.ToString(), AutomationRunStatus.Succeeded, finishedAt: now.AddDays(-15)),
            Run(recentId, recentId.ToString(), AutomationRunStatus.Failed, finishedAt: now.AddDays(-1))
        };

        RunCleanupHelper.SelectHistoryPurgeRuns(runs, now, TeardownRetention)
            .Select(r => r.RunId)
            .Should().Equal(oldId);
    }

    [Fact]
    public void History_purge_excludes_active_runs_even_when_old()
    {
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z");
        var activeId = Guid.NewGuid();
        var active = Run(activeId, activeId.ToString(), AutomationRunStatus.Running, finishedAt: null);
        active.CreatedAt = now.AddDays(-20);
        active.StartedAt = now.AddDays(-20);

        RunCleanupHelper.SelectHistoryPurgeRuns([active], now, TeardownRetention)
            .Should().BeEmpty();
    }

    [Fact]
    public void Custom_range_facilities_exclude_guid_tenants_without_an_automation_run()
    {
        var runId = Guid.NewGuid();
        var extra = Guid.NewGuid().ToString();
        var runs = new[]
        {
            Run(runId, runId.ToString(), AutomationRunStatus.Succeeded, finishedAt: DateTimeOffset.Parse("2026-08-10T12:00:00Z"))
        };

        RunCleanupHelper.SelectAutomationFacilitiesForRuns(
            Facilities(runId.ToString(), extra, "echs"),
            runs).Should().Equal(runId.ToString());
    }

    [Fact]
    public void Custom_range_selects_terminal_runs_in_utc_window()
    {
        var from = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-08-15T00:00:00Z");
        var inRange = Guid.NewGuid();
        var outOfRange = Guid.NewGuid();
        var runs = new[]
        {
            Run(inRange, inRange.ToString(), AutomationRunStatus.Cancelled, finishedAt: DateTimeOffset.Parse("2026-08-10T12:00:00Z")),
            Run(outOfRange, outOfRange.ToString(), AutomationRunStatus.Succeeded, finishedAt: DateTimeOffset.Parse("2026-08-20T12:00:00Z"))
        };

        RunCleanupHelper.SelectRunsFinishedInRange(runs, from, to)
            .Select(r => r.RunId)
            .Should().Equal(inRange);
    }

    [Fact]
    public void RequireFacilityList_throws_when_tenant_list_is_not_success()
    {
        var act = () => RunCleanupHelper.RequireFacilityList(new LinkApiResponse<Dictionary<string, string>>
        {
            StatusCode = 500
        });

        act.Should().Throw<InvalidOperationException>().WithMessage("*HTTP 500*");
    }

    [Fact]
    public void RequireFacilityList_treats_204_as_empty()
    {
        RunCleanupHelper.RequireFacilityList(new LinkApiResponse<Dictionary<string, string>>
        {
            StatusCode = 204
        }).Should().BeEmpty();
    }

    [Fact]
    public void EnsureTenantFacilityRemoved_throws_when_facility_still_present()
    {
        var remaining = new LinkApiResponse<FacilityModel>
        {
            StatusCode = 200,
            Body = new FacilityModel { FacilityId = "leftover", IsDeleted = false }
        };

        var act = () => RunCleanupHelper.EnsureTenantFacilityRemoved(remaining, "leftover");
        act.Should().Throw<InvalidOperationException>().WithMessage("*left facility 'leftover' in place*");
    }

    [Fact]
    public void EnsureTenantFacilityRemoved_accepts_404_and_soft_deleted()
    {
        RunCleanupHelper.EnsureTenantFacilityRemoved(
            new LinkApiResponse<FacilityModel> { StatusCode = 404 },
            "gone");

        RunCleanupHelper.EnsureTenantFacilityRemoved(
            new LinkApiResponse<FacilityModel>
            {
                StatusCode = 200,
                Body = new FacilityModel { FacilityId = "gone", IsDeleted = true }
            },
            "gone");

        RunCleanupHelper.EnsureTenantFacilityRemoved(
            new LinkApiResponse<FacilityModel> { StatusCode = 204 },
            "gone");
    }

    [Fact]
    public void EnsureTenantFacilityRemoved_throws_when_tenant_forbids_the_get()
    {
        var act = () => RunCleanupHelper.EnsureTenantFacilityRemoved(
            new LinkApiResponse<FacilityModel> { StatusCode = 403 },
            "leftover");

        act.Should().Throw<InvalidOperationException>().WithMessage("*HTTP 403*");
    }

    [Fact]
    public void EnsureTenantFacilityRemoved_throws_when_tenant_get_fails()
    {
        var act = () => RunCleanupHelper.EnsureTenantFacilityRemoved(
            new LinkApiResponse<FacilityModel> { StatusCode = 500 },
            "leftover");

        act.Should().Throw<InvalidOperationException>().WithMessage("*HTTP 500*");
    }

    [Fact]
    public async Task AbortAndQuiesce_without_deactivate_does_not_soft_delete_schedules()
    {
        var report = new Mock<IReportServiceClient>(MockBehavior.Strict);
        var da = new Mock<IDataAcquisitionServiceClient>();
        da.Setup(c => c.CancelAcquisitionLogsByFilterAsync(It.IsAny<object>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<DataAcquisitionBulkActionResultApiModel> { StatusCode = 200, Body = new() });
        var census = new Mock<ICensusServiceClient>();
        census.Setup(c => c.DisableFacilityJobsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse { StatusCode = 200 });

        await RunCleanupHelper.AbortAndQuiesceFacilityAsync(
            new InMemoryPipelineAbortRegistry(),
            da.Object,
            census.Object,
            report.Object,
            new NullOutput(),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            TimeSpan.FromDays(14),
            CancellationToken.None,
            deactivateSchedules: false);

        report.Verify(
            c => c.SoftDeleteScheduleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()),
            Times.Never);
        report.Verify(
            c => c.SetReportsDeletedStatusForFacilityAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CleanupCancelledRun_still_expunges_when_quiesce_fails()
    {
        var da = new Mock<IDataAcquisitionServiceClient>();
        da.Setup(c => c.CancelAcquisitionLogsByFilterAsync(It.IsAny<object>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DA down"));
        var census = new Mock<ICensusServiceClient>();
        census.Setup(c => c.DisableFacilityJobsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse { StatusCode = 200 });
        var report = new Mock<IReportServiceClient>();
        report.Setup(c => c.SoftDeleteScheduleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new LinkApiResponse { StatusCode = 200 });
        report.Setup(c => c.SetReportsDeletedStatusForFacilityAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse { StatusCode = 200 });

        var act = async () => await RunCleanupHelper.CleanupCancelledRunAsync(
            da.Object,
            census.Object,
            report.Object,
            new InMemoryPipelineAbortRegistry(),
            new FhirDataLoader("http://localhost"),
            new NullOutput(),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            TimeSpan.FromDays(14),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void IsAutomationFacilityId_accepts_guids_only()
    {
        RunCleanupHelper.IsAutomationFacilityId(Guid.NewGuid().ToString()).Should().BeTrue();
        RunCleanupHelper.IsAutomationFacilityId("b3310bbb-d0ab-4d49-8351-aabbee62662d").Should().BeTrue();
        RunCleanupHelper.IsAutomationFacilityId("echs").Should().BeFalse();
        RunCleanupHelper.IsAutomationFacilityId("census-b3310bbb-d0ab-4d49-8351-aabbee62662d-admit-24to48").Should().BeFalse();
        RunCleanupHelper.IsAutomationFacilityId(null).Should().BeFalse();
        RunCleanupHelper.IsAutomationFacilityId("").Should().BeFalse();
    }

    private static IReadOnlyList<string> SelectQuiesce(
        Dictionary<string, string> facilities,
        IReadOnlyList<AutomationRunSummary> runs,
        DateTimeOffset? now = null)
        => RunCleanupHelper.SelectQuiesceAutomationFacilities(
            facilities,
            runs,
            now ?? DateTimeOffset.Parse("2026-08-28T20:00:00Z"),
            QuiesceGrace);

    private static IReadOnlyList<string> SelectTeardown(
        Dictionary<string, string> facilities,
        IReadOnlyList<AutomationRunSummary> runs,
        DateTimeOffset? now = null)
        => RunCleanupHelper.SelectTeardownAutomationFacilities(
            facilities,
            runs,
            now ?? DateTimeOffset.Parse("2026-08-28T20:00:00Z"),
            TeardownRetention);

    private static Dictionary<string, string> Facilities(params string[] ids)
        => ids.ToDictionary(id => id, id => $"Facility {id}");

    private static AutomationRunSummary Run(
        Guid runId,
        string facilityId,
        AutomationRunStatus status,
        DateTimeOffset? finishedAt)
        => new()
        {
            RunId = runId,
            RunName = "test",
            Status = status,
            FacilityId = facilityId,
            CreatedAt = DateTimeOffset.Parse("2026-08-28T10:00:00Z"),
            FinishedAt = finishedAt,
        };

    private sealed class NullOutput : IAutomationOutput
    {
        public void WriteLine(string message) { }
        public void WriteLine(string format, params object[] args) { }
    }
}
