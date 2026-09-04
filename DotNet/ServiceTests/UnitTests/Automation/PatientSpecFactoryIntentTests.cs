using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Generation.Thetis;
using Thetis.Generation.Abstractions;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class PatientSpecFactoryIntentTests
{
    private static FhirGenerationCodes.ClinicalScenarioDefinition Pneumonia()
        => FhirGenerationCodes.GetScenarioById(ClinicalScenarioIds.Pneumonia.ToString())!;

    private static PatientProfile Profile(PatientGenerationIntent? intent, bool hypo = false)
        => new(
            new Dictionary<ProfiledMeasureType, MeasureEligibility>
            {
                [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying,
                [ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation] =
                    hypo ? MeasureEligibility.Qualifying : MeasureEligibility.NonQualifying
            },
            SeedOffset: 0,
            ClinicalScenarioId: ClinicalScenarioIds.Pneumonia.ToString(),
            ResourcesPerPatient: 50,
            Intent: intent);

    [Fact]
    public void Null_intent_matches_story_pack_defaults()
    {
        var scenario = Pneumonia();
        var withNull = PatientSpecFactory.From(Profile(null), scenario, 50);
        var explicitEmpty = PatientSpecFactory.From(Profile(new PatientGenerationIntent()), scenario, 50);

        Assert.Equal(scenario.PrimaryDxSnomed, withNull.PrimaryConditionSnomed);
        Assert.Equal("IMP", withNull.EncounterClass);
        Assert.Equal("32485007", withNull.EncounterType);
        Assert.Equal(withNull.PrimaryConditionSnomed, explicitEmpty.PrimaryConditionSnomed);
        Assert.Equal(withNull.ObservationCount, explicitEmpty.ObservationCount);
        Assert.Equal(withNull.DischargeDisposition, explicitEmpty.DischargeDisposition);
    }

    [Fact]
    public void ApplyIntent_overrides_demographics_encounter_and_primary_dx()
    {
        var spec = PatientSpecFactory.From(
            Profile(new PatientGenerationIntent
            {
                Gender = "female",
                MinAge = 70,
                MaxAge = 70,
                EncounterClass = "EMER",
                EncounterStatus = "in-progress",
                DischargeDisposition = "snf",
                PrimaryConditionSnomed = "84114007",
                PrimaryConditionDisplay = "Heart failure (disorder)"
            }),
            Pneumonia(),
            50);

        Assert.Equal("female", spec.PatientGender);
        Assert.Equal(70, spec.PatientMinAge);
        Assert.Equal(70, spec.PatientMaxAge);
        Assert.Equal("EMER", spec.EncounterClass);
        Assert.Equal("in-progress", spec.EncounterStatus);
        Assert.False(spec.IncludeHospitalization);
        Assert.Equal("snf", spec.DischargeDisposition);
        Assert.Equal("84114007", spec.PrimaryConditionSnomed);
        Assert.Equal("Heart failure (disorder)", spec.PrimaryConditionDisplay);
    }

    [Fact]
    public void Observation_palette_replace_and_exact_count_are_honored()
    {
        var spec = PatientSpecFactory.From(
            Profile(new PatientGenerationIntent
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
            }),
            Pneumonia(),
            80);

        Assert.Equal(3, spec.ObservationCount);
        Assert.Equal("718-7", Assert.Single(spec.ObservationPalette).LoincCode);
    }

    [Fact]
    public void Observation_palette_inherit_with_codes_replaces_story_pack()
    {
        var spec = PatientSpecFactory.From(
            Profile(new PatientGenerationIntent
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
            }),
            Pneumonia(),
            40);

        Assert.Equal("718-7", Assert.Single(spec.ObservationPalette).LoincCode);
    }

    [Fact]
    public void Replace_with_empty_palette_clears_story_codes()
    {
        var spec = PatientSpecFactory.From(
            Profile(new PatientGenerationIntent
            {
                ObservationPaletteMode = PaletteMode.Replace,
                ObservationPalette = [],
                ConditionPaletteMode = PaletteMode.Replace,
                ConditionPalette = [],
                ProcedurePaletteMode = PaletteMode.Replace,
                ProcedurePalette = []
            }),
            Pneumonia(),
            40);

        Assert.Empty(spec.ObservationPalette);
        Assert.Empty(spec.ConditionPalette);
        Assert.Empty(spec.ProcedurePalette);
    }

    [Fact]
    public void Condition_palette_append_keeps_story_codes()
    {
        var baseline = PatientSpecFactory.From(Profile(null), Pneumonia(), 40);
        var extra = new CodedPaletteItem { Code = "44054006", Display = "Diabetes mellitus type 2 (disorder)" };
        var spec = PatientSpecFactory.From(
            Profile(new PatientGenerationIntent
            {
                ConditionPaletteMode = PaletteMode.Append,
                ConditionPalette = [extra]
            }),
            Pneumonia(),
            40);

        Assert.Contains(spec.ConditionPalette, c => c.Code == extra.Code);
        Assert.True(spec.ConditionPalette.Count >= baseline.ConditionPalette.Count);
    }

    [Fact]
    public void Explicit_hypo_insulin_overrides_eligibility()
    {
        var forcedOn = PatientSpecFactory.From(
            Profile(new PatientGenerationIntent { IncludeHypoglycemicInsulin = true }, hypo: false),
            Pneumonia(),
            20);
        var forcedOff = PatientSpecFactory.From(
            Profile(new PatientGenerationIntent { IncludeHypoglycemicInsulin = false }, hypo: true),
            Pneumonia(),
            20);

        Assert.True(forcedOn.IncludeMedicationRequest);
        Assert.Equal(PatientSpecFactory.HypoInsulinMedicationIdVar, forcedOn.MedicationIdVar);
        Assert.False(forcedOff.IncludeMedicationRequest);
        Assert.Null(forcedOff.MedicationIdVar);
    }
}
