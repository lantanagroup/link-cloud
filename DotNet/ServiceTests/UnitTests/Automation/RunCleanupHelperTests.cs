using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Models;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class RunCleanupHelperTests
{
    private static readonly TimeSpan QuiesceGrace = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TeardownRetention = TimeSpan.FromDays(14);

    [Fact]
    public void Guid_facility_with_no_run_is_quiesced_and_torn_down()
    {
        var facilityId = Guid.NewGuid().ToString();
        var facilities = Facilities(facilityId);

        SelectQuiesce(facilities, []).Should().Equal(facilityId);
        SelectTeardown(facilities, []).Should().Equal(facilityId);
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

        leftovers.Should().HaveCount(1);
        leftovers.Should().NotContain(runId.ToString());
        leftovers.Should().NotContain(facilityId);
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
}
