using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using System.Reflection;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class MongoScenarioStoreTests
{
    [Fact]
    public void DeserializeCohorts_preserves_explicit_qualifying_when_all_measures_are_non_qualifying()
    {
        var json = """
            [
              {
                "PatientCount": 1,
                "CohortQualification": "Qualifying",
                "MeasureEligibilities": {
                  "NhsnAcuteCareHospitalMonthlyInitialPopulation": "NonQualifying"
                },
                "ResourcesPerPatientMin": 50,
                "ResourcesPerPatientMax": 50
              }
            ]
            """;

        var method = typeof(MongoScenarioStore)
            .GetMethod("DeserializeCohorts", BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var cohorts = (List<PatientCohortDefinition>)method!.Invoke(null, new object?[] { json })!;

        cohorts.Should().ContainSingle();
        cohorts[0].CohortQualification.Should().Be(MeasureEligibility.Qualifying);
        cohorts[0].GetEligibility(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            .Should().Be(MeasureEligibility.NonQualifying);
    }
}
