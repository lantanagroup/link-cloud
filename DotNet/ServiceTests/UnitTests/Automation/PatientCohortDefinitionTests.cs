using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class PatientCohortDefinitionTests
{
    [Fact]
    public void ExpandProfiles_uses_seed_plus_cohort_and_patient_index_for_resource_targets()
    {
        var measures = new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation };

        var cohorts = new List<PatientCohortDefinition>
        {
            new()
            {
                PatientCount = 2,
                MeasureEligibilities = measures.ToDictionary(m => m, _ => MeasureEligibility.Qualifying),
                ResourcesPerPatientMin = 50,
                ResourcesPerPatientMax = 100
            },
            new()
            {
                PatientCount = 1,
                MeasureEligibilities = measures.ToDictionary(m => m, _ => MeasureEligibility.Qualifying),
                ResourcesPerPatientMin = 50,
                ResourcesPerPatientMax = 100
            }
        };

        var profiles = PatientCohortDefinition.ExpandProfiles(cohorts, seed: 20260326);

        Assert.Equal(3, profiles.Count);

        var cohort1Patient1 = profiles[0].ResourcesPerPatient;
        var cohort1Patient2 = profiles[1].ResourcesPerPatient;
        var cohort2Patient1 = profiles[2].ResourcesPerPatient;

        Assert.NotNull(cohort1Patient1);
        Assert.NotNull(cohort1Patient2);
        Assert.NotNull(cohort2Patient1);

        Assert.NotEqual(cohort1Patient1, cohort1Patient2);
        Assert.NotEqual(cohort1Patient1, cohort2Patient1);
        Assert.NotEqual(cohort1Patient2, cohort2Patient1);

        Assert.InRange(cohort1Patient1!.Value, 50, 100);
        Assert.InRange(cohort1Patient2!.Value, 50, 100);
        Assert.InRange(cohort2Patient1!.Value, 50, 100);

        // Deterministic for same inputs.
        var secondPass = PatientCohortDefinition.ExpandProfiles(cohorts, seed: 20260326);
        Assert.Equal(
            profiles.Select(p => p.ResourcesPerPatient).ToArray(),
            secondPass.Select(p => p.ResourcesPerPatient).ToArray());
    }
}
