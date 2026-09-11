using FluentAssertions;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class ScheduledInpatientPatternCensusBehaviorTests
{
    [Theory]
    [InlineData(ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod, true, false, true)]
    [InlineData(ScheduledInpatientPattern.AdmittedBeforePeriodDischargedDuringPeriod, true, true, true)]
    [InlineData(ScheduledInpatientPattern.AdmittedDuringPeriodRemainsInpatientAfterPeriod, true, false, true)]
    [InlineData(ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod, true, true, true)]
    [InlineData(ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod, false, false, false)]
    [InlineData(ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod, false, false, false)]
    public void GetCensusBehavior_contract_is_unchanged(
        ScheduledInpatientPattern pattern,
        bool emitAdmit,
        bool emitDischarge,
        bool expectedInReport)
    {
        pattern.GetCensusBehavior().Should().Be(new ScheduledPatternCensusBehavior(emitAdmit, emitDischarge, expectedInReport));
    }
}
