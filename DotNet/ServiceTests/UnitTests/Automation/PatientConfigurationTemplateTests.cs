using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Generation.Thetis;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class PatientConfigurationTemplateTests
{
    [Fact]
    public void FromClinicalProfile_fills_visible_pneumonia_codes()
    {
        var scenario = FhirGenerationCodes.GetScenarioById(ClinicalScenarioIds.Pneumonia.ToString())!;
        var intent = PatientConfigurationTemplate.FromClinicalProfile(scenario, totalResourcesPerPatient: 50);

        Assert.Equal(scenario.PrimaryDxSnomed, intent.PrimaryConditionSnomed);
        Assert.Equal(scenario.PrimaryDxDisplay, intent.PrimaryConditionDisplay);
        Assert.Equal("IMP", intent.EncounterClass);
        Assert.Equal("finished", intent.EncounterStatus);
        Assert.Equal(scenario.DischargeDispositionCode, intent.DischargeDisposition);
        Assert.Equal(PaletteMode.Replace, intent.ObservationPaletteMode);
        Assert.NotEmpty(intent.ObservationPalette!);
        Assert.Contains(intent.ObservationPalette, o => !string.IsNullOrWhiteSpace(o.LoincCode));
        Assert.True(intent.IncludeConditionDrivenMedications);
        Assert.True(intent.GenerateLabWork);
        Assert.False(intent.IncludeHypoglycemicInsulin);
        Assert.Equal(18, intent.MinAge);
        Assert.Equal(80, intent.MaxAge);
    }

    [Fact]
    public void FromClinicalProfile_hypo_sets_insulin_flag()
    {
        var scenario = FhirGenerationCodes.GetScenarioById(ClinicalScenarioIds.DiabeticHypoglycemia.ToString())!;
        var intent = PatientConfigurationTemplate.FromClinicalProfile(scenario, hypo: true);

        Assert.True(intent.IncludeHypoglycemicInsulin);
        Assert.Equal(scenario.PrimaryDxSnomed, intent.PrimaryConditionSnomed);
    }

    [Fact]
    public void Seeded_intent_round_trips_through_the_factory()
    {
        var scenario = FhirGenerationCodes.GetScenarioById(ClinicalScenarioIds.Pneumonia.ToString())!;
        var seeded = PatientConfigurationTemplate.FromClinicalProfile(scenario, 50);
        var profile = new PatientProfile(
            new Dictionary<ProfiledMeasureType, MeasureEligibility>
            {
                [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying
            },
            ClinicalScenarioId: scenario.ScenarioId.ToString(),
            ResourcesPerPatient: 50,
            Intent: seeded);

        var spec = PatientSpecFactory.From(profile, scenario, 50);
        Assert.Equal(scenario.PrimaryDxSnomed, spec.PrimaryConditionSnomed);
        Assert.Equal("IMP", spec.EncounterClass);
        Assert.Equal(seeded.ObservationPalette!.Count, spec.ObservationPalette.Count);
    }
}
