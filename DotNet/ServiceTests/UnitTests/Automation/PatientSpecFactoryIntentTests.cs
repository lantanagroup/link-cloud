using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Generation.Thetis;
using Thetis.Generation.Abstractions;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class PatientSpecFactoryIntentTests
{
    private static FhirGenerationCodes.ClinicalScenarioDefinition Pneumonia()
        => FhirGenerationCodes.GetScenarioById(ClinicalScenarioIds.Pneumonia.ToString())!;

    private static PatientProfile Profile(PatientGenerationIntent? intent, bool hypo = false, string? clinicalScenarioId = null)
        => new(
            new Dictionary<ProfiledMeasureType, MeasureEligibility>
            {
                [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying,
                [ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation] =
                    hypo ? MeasureEligibility.Qualifying : MeasureEligibility.NonQualifying
            },
            SeedOffset: 0,
            ClinicalScenarioId: clinicalScenarioId,
            ResourcesPerPatient: 50,
            Intent: intent);

    private static PatientGenerationSpec Spec(PatientGenerationIntent? intent, int n = 50, bool hypo = false)
        => PatientSpecFactory.From(Profile(intent, hypo), n);

    [Fact]
    public void Blank_intent_uses_fixture_defaults_not_a_story_pack()
    {
        var withNull = Spec(null);
        var explicitEmpty = Spec(new PatientGenerationIntent());

        Assert.True(string.IsNullOrWhiteSpace(withNull.PrimaryConditionSnomed));
        Assert.Empty(withNull.ObservationPalette);
        Assert.Empty(withNull.ConditionPalette);
        Assert.Empty(withNull.ProcedurePalette);
        Assert.Equal("IMP", withNull.EncounterClass);
        Assert.Equal("32485007", withNull.EncounterType);
        Assert.Equal("home", withNull.DischargeDisposition);
        Assert.False(withNull.IncludeConditionDrivenMedications);
        Assert.False(withNull.IncludeMedicationRequest);
        Assert.False(withNull.GenerateLabWork);
        Assert.Equal(0, withNull.MedicationAdministrationCount);
        Assert.True(string.IsNullOrWhiteSpace(withNull.MedicationAdministrationRxNorm));
        Assert.Equal(withNull.EncounterClass, explicitEmpty.EncounterClass);
        Assert.Equal(withNull.ObservationPalette.Count, explicitEmpty.ObservationPalette.Count);
    }

    [Fact]
    public void ClinicalScenarioId_is_ignored_when_intent_is_blank()
    {
        var spec = PatientSpecFactory.From(
            Profile(null, clinicalScenarioId: ClinicalScenarioIds.Pneumonia.ToString()),
            40);

        Assert.True(string.IsNullOrWhiteSpace(spec.PrimaryConditionSnomed));
        Assert.Empty(spec.ObservationPalette);
        Assert.Empty(spec.ConditionPalette);
        Assert.False(spec.IncludeConditionDrivenMedications);
    }

    [Fact]
    public void Kit_intent_is_the_source_of_truth()
    {
        var scenario = Pneumonia();
        var kit = PatientConfigurationTemplate.FromClinicalProfile(scenario, 50);
        var spec = Spec(kit);

        Assert.Equal(scenario.PrimaryDxSnomed, spec.PrimaryConditionSnomed);
        Assert.Equal("IMP", spec.EncounterClass);
        Assert.Equal("32485007", spec.EncounterType);
        Assert.NotEmpty(spec.ObservationPalette);
        Assert.True(spec.IncludeConditionDrivenMedications);
        Assert.True(spec.GenerateLabWork);
        Assert.False(spec.IncludeMedicationRequest);
    }

    [Fact]
    public void ApplyIntent_overrides_demographics_encounter_and_primary_dx()
    {
        var spec = Spec(new PatientGenerationIntent
        {
            Gender = "female",
            MinAge = 70,
            MaxAge = 70,
            EncounterClass = "EMER",
            EncounterStatus = "in-progress",
            DischargeDisposition = "snf",
            PrimaryConditionSnomed = "84114007",
            PrimaryConditionDisplay = "Heart failure (disorder)"
        });

        Assert.Equal("female", spec.PatientGender);
        Assert.Equal(70, spec.PatientMinAge);
        Assert.Equal(70, spec.PatientMaxAge);
        Assert.Equal("EMER", spec.EncounterClass);
        Assert.Null(spec.EncounterType);
        Assert.Equal("in-progress", spec.EncounterStatus);
        Assert.False(spec.IncludeHospitalization);
        Assert.Equal("snf", spec.DischargeDisposition);
        Assert.Equal("84114007", spec.PrimaryConditionSnomed);
        Assert.Equal("Heart failure (disorder)", spec.PrimaryConditionDisplay);
    }

    [Fact]
    public void Ambulatory_class_clears_inpatient_encounter_type()
    {
        var spec = Spec(new PatientGenerationIntent { EncounterClass = "AMB" }, n: 20);

        Assert.Equal("AMB", spec.EncounterClass);
        Assert.Null(spec.EncounterType);
    }

    [Fact]
    public void Pinned_zero_medication_requests_turns_off_condition_driven_meds()
    {
        var spec = Spec(new PatientGenerationIntent
        {
            IncludeConditionDrivenMedications = true,
            ResourceTypeCounts = new Dictionary<string, int> { ["MedicationRequest"] = 0 }
        }, n: 20);

        Assert.Equal(0, spec.MedicationRequestCount);
        Assert.False(spec.IncludeConditionDrivenMedications);
    }

    [Fact]
    public void Observation_palette_replace_and_exact_count_are_honored()
    {
        var spec = Spec(new PatientGenerationIntent
        {
            ObservationPaletteMode = PaletteMode.Replace,
            ObservationPalette =
            [
                new ObservationPaletteItem
                {
                    LoincCode = "718-7",
                    LoincDisplay = "Hemoglobin",
                    Type = "laboratory",
                    Unit = "g/dL",
                    MinValue = 12,
                    MaxValue = 17
                }
            ],
            ResourceTypeCounts = new Dictionary<string, int> { ["Observation"] = 3 }
        }, n: 80);

        Assert.Equal(3, spec.ObservationCount);
        Assert.Equal("718-7", Assert.Single(spec.ObservationPalette).LoincCode);
    }

    [Fact]
    public void Observation_palette_inherit_with_codes_uses_those_codes()
    {
        var spec = Spec(new PatientGenerationIntent
        {
            ObservationPaletteMode = PaletteMode.Inherit,
            ObservationPalette =
            [
                new ObservationPaletteItem
                {
                    LoincCode = "718-7",
                    LoincDisplay = "Hemoglobin",
                    Type = "laboratory"
                }
            ]
        }, n: 40);

        Assert.Equal("718-7", Assert.Single(spec.ObservationPalette).LoincCode);
    }

    [Fact]
    public void Replace_with_empty_palette_clears_codes()
    {
        var kit = PatientConfigurationTemplate.FromClinicalProfile(Pneumonia(), 40);
        var spec = Spec(new PatientGenerationIntent
        {
            ObservationPaletteMode = PaletteMode.Replace,
            ObservationPalette = [],
            ConditionPaletteMode = PaletteMode.Replace,
            ConditionPalette = [],
            ProcedurePaletteMode = PaletteMode.Replace,
            ProcedurePalette = []
        }, n: 40);

        Assert.Empty(spec.ObservationPalette);
        Assert.Empty(spec.ConditionPalette);
        Assert.Empty(spec.ProcedurePalette);

        var clearedKit = PatientGenerationIntent.Merge(kit, new PatientGenerationIntent
        {
            ObservationPaletteMode = PaletteMode.Replace,
            ObservationPalette = [],
            ConditionPaletteMode = PaletteMode.Replace,
            ConditionPalette = [],
            ProcedurePaletteMode = PaletteMode.Replace,
            ProcedurePalette = []
        });
        var clearedSpec = Spec(clearedKit, n: 40);
        Assert.Empty(clearedSpec.ObservationPalette);
        Assert.Empty(clearedSpec.ConditionPalette);
        Assert.Empty(clearedSpec.ProcedurePalette);
    }

    [Fact]
    public void Condition_palette_append_merges_onto_kit_codes()
    {
        var kit = PatientConfigurationTemplate.FromClinicalProfile(Pneumonia(), 40);
        var extra = new CodedPaletteItem { Code = "44054006", Display = "Diabetes mellitus type 2 (disorder)" };
        var merged = PatientGenerationIntent.Merge(kit, new PatientGenerationIntent
        {
            ConditionPaletteMode = PaletteMode.Append,
            ConditionPalette = [extra]
        });
        var spec = Spec(merged, n: 40);

        Assert.Contains(spec.ConditionPalette, c => c.Code == extra.Code);
        Assert.True(spec.ConditionPalette.Count >= kit.ConditionPalette!.Count);
        Assert.Equal(kit.PrimaryConditionSnomed, spec.PrimaryConditionSnomed);
    }

    [Fact]
    public void Explicit_hypo_insulin_overrides_eligibility()
    {
        var forcedOn = Spec(new PatientGenerationIntent { IncludeHypoglycemicInsulin = true }, n: 20, hypo: false);
        var forcedOff = Spec(new PatientGenerationIntent { IncludeHypoglycemicInsulin = false }, n: 20, hypo: true);

        Assert.True(forcedOn.IncludeMedicationRequest);
        Assert.Equal(PatientSpecFactory.HypoInsulinMedicationIdVar, forcedOn.MedicationIdVar);
        Assert.False(forcedOff.IncludeMedicationRequest);
        Assert.Null(forcedOff.MedicationIdVar);
    }

    [Fact]
    public void Diabetes_med_admin_code_turns_insulin_pair_on()
    {
        var spec = Spec(new PatientGenerationIntent
        {
            MedicationAdministrationRxNorm = "1116635",
            MedicationAdministrationDisplay = "Insulin glargine"
        }, n: 20);

        Assert.True(spec.IncludeMedicationRequest);
        Assert.Equal("1116635", spec.MedicationAdministrationRxNorm);
    }
}
