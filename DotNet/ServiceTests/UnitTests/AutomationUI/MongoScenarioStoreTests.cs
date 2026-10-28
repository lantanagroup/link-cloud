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

    [Fact]
    public void DeserializeCohorts_reads_patient_configuration_id_and_intent()
    {
        var json = """
            [
              {
                "PatientCount": 2,
                "CohortQualification": "Qualifying",
                "MeasureEligibilities": {
                  "NhsnAcuteCareHospitalMonthlyInitialPopulation": "Qualifying"
                },
                "ResourcesPerPatientMin": 50,
                "ResourcesPerPatientMax": 50,
                "PatientConfigurationId": "00000000-0000-0000-3000-000000000001",
                "Intent": {
                  "Gender": "female",
                  "MinAge": 70,
                  "ConditionPaletteMode": "Replace"
                }
              }
            ]
            """;

        var method = typeof(MongoScenarioStore)
            .GetMethod("DeserializeCohorts", BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var cohorts = (List<PatientCohortDefinition>)method!.Invoke(null, new object?[] { json })!;

        cohorts.Should().ContainSingle();
        cohorts[0].PatientConfigurationId.Should().Be(Guid.Parse("00000000-0000-0000-3000-000000000001"));
        cohorts[0].Intent.Should().NotBeNull();
        cohorts[0].Intent!.Gender.Should().Be("female");
        cohorts[0].Intent.MinAge.Should().Be(70);
        cohorts[0].Intent.ConditionPaletteMode.Should().Be(PaletteMode.Replace);
    }

    [Fact]
    public void DeserializeCohorts_old_payload_without_config_fields_is_quick_setup()
    {
        var json = """
            [
              {
                "PatientCount": 1,
                "CohortQualification": "Qualifying",
                "MeasureEligibilities": {
                  "NhsnAcuteCareHospitalMonthlyInitialPopulation": "Qualifying"
                },
                "ResourcesPerPatientMin": 50,
                "ResourcesPerPatientMax": 50
              }
            ]
            """;

        var method = typeof(MongoScenarioStore)
            .GetMethod("DeserializeCohorts", BindingFlags.Static | BindingFlags.NonPublic);

        var cohorts = (List<PatientCohortDefinition>)method!.Invoke(null, new object?[] { json })!;

        cohorts.Should().ContainSingle();
        cohorts[0].PatientConfigurationId.Should().BeNull();
        cohorts[0].Intent.Should().BeNull();
    }
}
