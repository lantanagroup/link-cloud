using Automation.UI.Services;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RunExecutorDmrpMeasureNameTests
{
    [Theory]
    [InlineData("NHSNAcuteCareHospitalMonthlyInitialPopulation")]
    [InlineData("NHSNGlycemicControlHypoglycemicInitialPopulation")]
    public void GetDmrpMeasureName_preserves_short_measure_ids(string measureId)
    {
        RunExecutor.GetDmrpMeasureName(measureId).Should().Be(measureId);
    }

    [Fact]
    public void GetDmrpMeasureName_preserves_exactly_50_characters()
    {
        var measureId = new string('A', 50);

        RunExecutor.GetDmrpMeasureName(measureId).Should().Be(measureId);
    }

    [Theory]
    [InlineData("NHSNRespiratoryPathogensSurveillanceInitialPopulation")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void GetDmrpMeasureName_shortens_long_ids_deterministically(string measureId)
    {
        var name = RunExecutor.GetDmrpMeasureName(measureId);

        name.Should().HaveLength(50);
        name.Should().StartWith(measureId[..33] + "-");
        RunExecutor.GetDmrpMeasureName(measureId).Should().Be(name);
    }

    [Fact]
    public void GetDmrpMeasureName_distinguishes_ids_with_the_same_prefix()
    {
        var prefix = new string('A', 50);

        RunExecutor.GetDmrpMeasureName(prefix + "B").Should()
            .NotBe(RunExecutor.GetDmrpMeasureName(prefix + "C"));
    }
}
