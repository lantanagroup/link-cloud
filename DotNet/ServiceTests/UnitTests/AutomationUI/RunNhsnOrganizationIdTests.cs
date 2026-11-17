using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RunNhsnOrganizationIdTests
{
    [Fact]
    public void Before_the_facility_is_recorded_the_org_id_is_the_run_id()
    {
        var runId = Guid.NewGuid();
        var run = new AutomationRunSummary { RunId = runId };

        RunNhsnOrganizationId.For(run).Should().Be(runId.ToString());
    }

    [Fact]
    public void After_the_facility_is_recorded_the_org_id_is_that_facility_id()
    {
        var run = new AutomationRunSummary
        {
            RunId = Guid.NewGuid(),
            FacilityId = Guid.NewGuid().ToString()
        };

        RunNhsnOrganizationId.For(run).Should().Be(run.FacilityId);
    }
}
