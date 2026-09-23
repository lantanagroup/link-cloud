using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class ApiStabilitySeedMissTests
{
    [Theory]
    [InlineData("API health run failed. error=1 validator(s) failed:\n- REPORT INTERNAL ABS MANIFEST VALIDATION failed with 3 issue(s): - ABS patient=Patient-44287a1e-001, type=ServiceRequest: expected=2 (sim-acquired ? reachable-CQL + derived), actual=0. | - ABS artifacts missing expected resource: ServiceRequest/Patient-44287a1e-001-SvcReq-001")]
    [InlineData("REPORT INTERNAL ABS MANIFEST VALIDATION: ABS patient=Patient-1, type=Observation: expected=4, actual=3. | ABS artifacts missing expected resource: Observation/Patient-1-Obs-001")]
    public void KnownSeedAcquisitionMiss_IsRetryable(string error)
    {
        ApiStabilitySeedMiss.IsRetryable(error).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("API health run failed. status=Failed, failedEndpoints=2, error=GET /facilities returned 500")]
    [InlineData("REPORT INTERNAL ABS MANIFEST VALIDATION: ABS patient=Patient-1, type=Condition: expected=1, actual=0.")]
    [InlineData("ABS artifacts missing expected resource: ServiceRequest/Patient-1-SvcReq-001")]
    public void OtherFailures_AreNotRetried(string? error)
    {
        ApiStabilitySeedMiss.IsRetryable(error).Should().BeFalse();
    }
}
