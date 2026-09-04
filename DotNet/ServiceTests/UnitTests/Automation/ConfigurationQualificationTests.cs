using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Generation.Thetis;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class ConfigurationQualificationTests
{
    private static readonly ProfiledMeasureType Ach = ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation;
    private static readonly ProfiledMeasureType AchDaily = ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation;
    private static readonly ProfiledMeasureType Hypo = ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation;

    private static FhirGenerationCodes.ClinicalScenarioDefinition Pneumonia()
        => FhirGenerationCodes.GetScenarioById(ClinicalScenarioIds.Pneumonia.ToString())!;

    private static FhirGenerationCodes.ClinicalScenarioDefinition DiabeticHypo()
        => FhirGenerationCodes.GetScenarioById(ClinicalScenarioIds.DiabeticHypoglycemia.ToString())!;

    [Fact]
    public void Inpatient_pneumonia_qualifies_ach_not_hypo()
    {
        var prediction = ConfigurationQualification.Predict(
            new PatientGenerationIntent { EncounterClass = "IMP", IncludeHypoglycemicInsulin = false },
            Pneumonia());

        Assert.Equal(MeasureEligibility.Qualifying, prediction.MeasureEligibilities[Ach]);
        Assert.Equal(MeasureEligibility.Qualifying, prediction.MeasureEligibilities[AchDaily]);
        Assert.Equal(MeasureEligibility.NonQualifying, prediction.MeasureEligibilities[Hypo]);
        Assert.True(prediction.CensusPlacesInReport);
        Assert.True(prediction.ExpectedInReport([Ach]));
        Assert.False(prediction.ExpectedInReport([Hypo]));
    }

    [Fact]
    public void Diabetic_hypoglycemia_profile_qualifies_ach_and_hypo()
    {
        var intent = PatientConfigurationTemplate.FromClinicalProfile(DiabeticHypo(), 50, inpatient: true, hypo: true);
        var prediction = ConfigurationQualification.Predict(intent, DiabeticHypo());

        Assert.Equal(MeasureEligibility.Qualifying, prediction.MeasureEligibilities[Ach]);
        Assert.Equal(MeasureEligibility.Qualifying, prediction.MeasureEligibilities[Hypo]);
    }

    [Fact]
    public void Ambulatory_pneumonia_qualifies_for_neither()
    {
        var prediction = ConfigurationQualification.Predict(
            new PatientGenerationIntent { EncounterClass = "AMB", IncludeHypoglycemicInsulin = false },
            Pneumonia());

        Assert.Equal(MeasureEligibility.NonQualifying, prediction.MeasureEligibilities[Ach]);
        Assert.Equal(MeasureEligibility.NonQualifying, prediction.MeasureEligibilities[Hypo]);
        Assert.False(prediction.ExpectedInReport([Ach]));
    }

    [Fact]
    public void Emergency_plus_insulin_is_ach_not_hypo()
    {
        var prediction = ConfigurationQualification.Predict(
            new PatientGenerationIntent { EncounterClass = "EMER", IncludeHypoglycemicInsulin = true },
            Pneumonia());

        Assert.Equal(MeasureEligibility.Qualifying, prediction.MeasureEligibilities[Ach]);
        Assert.Equal(MeasureEligibility.NonQualifying, prediction.MeasureEligibilities[Hypo]);
    }

    [Fact]
    public void Turning_insulin_off_on_diabetic_profile_drops_hypo()
    {
        var prediction = ConfigurationQualification.Predict(
            new PatientGenerationIntent { EncounterClass = "IMP", IncludeHypoglycemicInsulin = false },
            DiabeticHypo());

        Assert.Equal(MeasureEligibility.Qualifying, prediction.MeasureEligibilities[Ach]);
        Assert.Equal(MeasureEligibility.NonQualifying, prediction.MeasureEligibilities[Hypo]);
    }

    [Fact]
    public void Out_of_window_pattern_keeps_ip_but_excludes_from_report()
    {
        var prediction = ConfigurationQualification.Predict(
            new PatientGenerationIntent { EncounterClass = "IMP" },
            Pneumonia(),
            pattern: ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod);

        Assert.Equal(MeasureEligibility.Qualifying, prediction.MeasureEligibilities[Ach]);
        Assert.False(prediction.CensusPlacesInReport);
        Assert.False(prediction.ExpectedInReport([Ach]));
    }

    [Fact]
    public void Empty_intent_defaults_to_inpatient_story_pack()
    {
        var pneumonia = ConfigurationQualification.Predict(null, Pneumonia());
        Assert.Equal(MeasureEligibility.Qualifying, pneumonia.MeasureEligibilities[Ach]);
        Assert.Equal(MeasureEligibility.NonQualifying, pneumonia.MeasureEligibilities[Hypo]);

        var hypo = ConfigurationQualification.Predict(null, DiabeticHypo());
        Assert.Equal(MeasureEligibility.Qualifying, hypo.MeasureEligibilities[Ach]);
        Assert.Equal(MeasureEligibility.Qualifying, hypo.MeasureEligibilities[Hypo]);
    }

    [Fact]
    public void Prediction_matches_what_the_spec_factory_will_emit()
    {
        var amb = new PatientGenerationIntent { EncounterClass = "AMB", IncludeHypoglycemicInsulin = false };
        var spec = PatientSpecFactory.From(
            new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>(), Intent: amb),
            Pneumonia(),
            20);
        var prediction = ConfigurationQualification.Predict(amb, Pneumonia());

        Assert.Equal("AMB", spec.EncounterClass);
        Assert.False(spec.IncludeMedicationRequest);
        Assert.Equal(MeasureEligibility.NonQualifying, prediction.MeasureEligibilities[Ach]);
        Assert.Equal(MeasureEligibility.NonQualifying, prediction.MeasureEligibilities[Hypo]);

        var diabeticIntent = PatientConfigurationTemplate.FromClinicalProfile(DiabeticHypo(), 20, inpatient: true, hypo: true);
        var hypoSpec = PatientSpecFactory.From(
            new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>(), Intent: diabeticIntent),
            DiabeticHypo(),
            20);
        var hypoPrediction = ConfigurationQualification.Predict(diabeticIntent, DiabeticHypo());

        Assert.Equal("IMP", hypoSpec.EncounterClass);
        Assert.True(hypoSpec.IncludeMedicationRequest);
        Assert.Equal(MeasureEligibility.Qualifying, hypoPrediction.MeasureEligibilities[Ach]);
        Assert.Equal(MeasureEligibility.Qualifying, hypoPrediction.MeasureEligibilities[Hypo]);
    }
}
