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
}
