using LantanaGroup.Link.PatientFlowSimulator.Scenario;

namespace UnitTests.PatientFlowSimulator;

[Trait("Category", "UnitTests")]
public class ScenarioDefinitionTests
{
    [Fact]
    public void Load_DefaultScenario_HasHundredPatientsAcrossThreeFacilities()
    {
        var scenario = DefaultScenario.Load();

        Assert.Equal(3, scenario.Facilities.Count);
        Assert.Equal(100, scenario.Facilities.Sum(f => f.PatientsPerMonth));
    }

    [Fact]
    public void Load_DefaultScenario_CoversAllFourMeasures()
    {
        var scenario = DefaultScenario.Load();

        var measures = scenario.Facilities.SelectMany(f => f.Measures).Distinct().ToList();

        Assert.Equal(4, measures.Count);
        Assert.Contains("NHSNAcuteCareHospitalMonthlyInitialPopulation", measures);
        Assert.Contains("NHSNAcuteCareHospitalDailyInitialPopulation", measures);
        Assert.Contains("NHSNGlycemicControlHypoglycemicInitialPopulation", measures);
        Assert.Contains("NHSNLongTermCareMonthlyInitialPopulation", measures);
    }

    [Fact]
    public void Validate_CohortWithUnknownTemplate_Throws()
    {
        var scenario = DefaultScenario.Load();
        scenario.Facilities[0].Cohorts[0].Template = "does-not-exist";

        var ex = Assert.Throws<InvalidDataException>(scenario.Validate);

        Assert.Contains("does-not-exist", ex.Message);
    }

    [Fact]
    public void Validate_CrossMonthCountAboveCount_Throws()
    {
        var scenario = DefaultScenario.Load();
        scenario.Facilities[0].Cohorts[0].CrossMonthCount = scenario.Facilities[0].Cohorts[0].Count + 1;

        Assert.Throws<InvalidDataException>(scenario.Validate);
    }
}
