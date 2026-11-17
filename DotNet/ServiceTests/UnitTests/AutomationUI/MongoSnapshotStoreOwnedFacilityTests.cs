using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class MongoSnapshotStoreOwnedFacilityTests
{
    [Fact]
    public void Same_facility_and_run_id_is_written_once()
    {
        var runId = Guid.NewGuid();
        var summary = new AutomationRunSummary
        {
            RunId = runId,
            FacilityId = runId.ToString(),
            AutomationCreatedFacility = true
        };

        MongoSnapshotStore.DistinctOwnedFacilityIds(summary).Should().Equal(runId.ToString());
    }

    [Fact]
    public void Created_facility_and_run_id_are_both_kept()
    {
        var runId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        var summary = new AutomationRunSummary
        {
            RunId = runId,
            FacilityId = facilityId,
            AutomationCreatedFacility = true
        };

        MongoSnapshotStore.DistinctOwnedFacilityIds(summary).Should().Equal(facilityId, runId.ToString());
    }

    [Fact]
    public void Five_digit_facility_is_kept_and_the_run_id_is_not()
    {
        var summary = new AutomationRunSummary
        {
            RunId = Guid.NewGuid(),
            FacilityId = "48291",
            AutomationCreatedFacility = true
        };

        MongoSnapshotStore.DistinctOwnedFacilityIds(summary).Should().Equal("48291");
    }

    [Fact]
    public void Five_digit_facility_without_the_created_marker_owns_nothing()
    {
        var summary = new AutomationRunSummary
        {
            RunId = Guid.NewGuid(),
            FacilityId = "48291"
        };

        MongoSnapshotStore.DistinctOwnedFacilityIds(summary).Should().BeEmpty();
    }
}
