using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class PatientConfigurationHydratorTests
{
    [Fact]
    public async Task Hydrate_merges_prefab_intent_then_cohort_overlay()
    {
        var configId = Guid.Parse("00000000-0000-0000-3000-000000000099");
        var store = new Mock<IPatientConfigurationStore>();
        store.Setup(s => s.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PatientConfiguration
            {
                Id = configId,
                Name = "Prefab",
                ClinicalScenarioIds = [ClinicalScenarioIds.Pneumonia.ToString()],
                ResourcesPerPatientMin = 20,
                ResourcesPerPatientMax = 30,
                ScheduledInpatientPattern = ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod,
                Intent = new PatientGenerationIntent
                {
                    Gender = "female",
                    DischargeDisposition = "home"
                }
            });

        var options = new ResolvedRunOptions(
            PatientCount: 1,
            ResourcesPerPatient: 50,
            Seed: 7,
            PollingIntervalSeconds: 3,
            MaxPollingDurationMinutes: 10,
            LokiScrapeWindowMinutes: 10,
            CleanupServiceData: false,
            CleanupFhirData: true,
            SelectedMeasures: [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation],
            PatientProfiles: [],
            PatientCohorts:
            [
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    PatientConfigurationId = configId,
                    ResourcesPerPatientMin = 50,
                    ResourcesPerPatientMax = 100,
                    MeasureEligibilities =
                    {
                        [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying
                    },
                    Intent = new PatientGenerationIntent { DischargeDisposition = "snf", MinAge = 70 }
                }
            ],
            ReportMethod: ReportMethod.Adhoc);

        var hydrated = await PatientConfigurationHydrator.HydrateAsync(options, store.Object, CancellationToken.None);

        var cohort = Assert.Single(hydrated.PatientCohorts);
        Assert.Equal("female", cohort.Intent!.Gender);
        Assert.Equal("snf", cohort.Intent.DischargeDisposition);
        Assert.Equal(70, cohort.Intent.MinAge);
        Assert.Equal(ClinicalScenarioIds.Pneumonia.ToString(), Assert.Single(cohort.EligibleClinicalScenarioIds));
        Assert.Equal(20, cohort.ResourcesPerPatientMin);
        Assert.Equal(30, cohort.ResourcesPerPatientMax);
        Assert.Equal(ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod, cohort.ScheduledInpatientPattern);

        var profile = Assert.Single(hydrated.PatientProfiles);
        Assert.Equal("female", profile.Intent!.Gender);
        Assert.Equal("snf", profile.Intent.DischargeDisposition);
    }

    [Fact]
    public async Task Hydrate_leaves_quick_setup_cohorts_unchanged()
    {
        var store = new Mock<IPatientConfigurationStore>(MockBehavior.Strict);
        var options = new ResolvedRunOptions(
            PatientCount: 1,
            ResourcesPerPatient: 50,
            Seed: 7,
            PollingIntervalSeconds: 3,
            MaxPollingDurationMinutes: 10,
            LokiScrapeWindowMinutes: 10,
            CleanupServiceData: false,
            CleanupFhirData: true,
            SelectedMeasures: [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation],
            PatientProfiles: [],
            PatientCohorts:
            [
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    ResourcesPerPatientMin = 50,
                    ResourcesPerPatientMax = 50,
                    MeasureEligibilities =
                    {
                        [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying
                    }
                }
            ],
            ReportMethod: ReportMethod.Adhoc);

        var hydrated = await PatientConfigurationHydrator.HydrateAsync(options, store.Object, CancellationToken.None);

        Assert.Null(Assert.Single(hydrated.PatientCohorts).Intent);
        store.Verify(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
