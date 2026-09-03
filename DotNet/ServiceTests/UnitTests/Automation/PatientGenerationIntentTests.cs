using LantanaGroup.Automation.Generation;
using Thetis.Generation.Abstractions;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class PatientGenerationIntentTests
{
    [Fact]
    public void Empty_intent_has_no_overlays()
    {
        Assert.False(new PatientGenerationIntent().HasOverlays());
    }

    [Fact]
    public void Merge_prefers_non_null_overlay_fields()
    {
        var under = new PatientGenerationIntent
        {
            Gender = "female",
            MinAge = 40,
            DischargeDisposition = "home",
            IncludeHypoglycemicInsulin = true
        };
        var over = new PatientGenerationIntent
        {
            Gender = "male",
            DischargeDisposition = null,
            MaxAge = 70
        };

        var merged = PatientGenerationIntent.Merge(under, over)!;

        Assert.Equal("male", merged.Gender);
        Assert.Equal(40, merged.MinAge);
        Assert.Equal(70, merged.MaxAge);
        Assert.Equal("home", merged.DischargeDisposition);
        Assert.True(merged.IncludeHypoglycemicInsulin);
        Assert.True(merged.HasOverlays());
    }

    [Fact]
    public void Clone_copies_palettes_and_counts()
    {
        var source = new PatientGenerationIntent
        {
            ConditionPaletteMode = PaletteMode.Replace,
            ConditionPalette = [new CodedPaletteItem { Code = "44054006", Display = "T2DM" }],
            ResourceTypeCounts = new Dictionary<string, int> { ["Observation"] = 3 }
        };

        var clone = PatientGenerationIntent.Clone(source)!;
        source.ConditionPalette![0] = new CodedPaletteItem { Code = "changed", Display = "changed" };
        source.ResourceTypeCounts!["Observation"] = 9;

        Assert.Equal("44054006", clone.ConditionPalette![0].Code);
        Assert.Equal(3, clone.ResourceTypeCounts!["Observation"]);
        Assert.Equal(PaletteMode.Replace, clone.ConditionPaletteMode);
    }

    [Fact]
    public void ExpandProfiles_copies_intent_onto_each_profile()
    {
        var intent = new PatientGenerationIntent { Gender = "female", MinAge = 70, MaxAge = 70 };
        var cohorts = new List<PatientCohortDefinition>
        {
            new()
            {
                PatientCount = 2,
                MeasureEligibilities =
                {
                    [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying
                },
                ResourcesPerPatientMin = 10,
                ResourcesPerPatientMax = 10,
                Intent = intent
            }
        };

        var profiles = PatientCohortDefinition.ExpandProfiles(cohorts, seed: 1);

        Assert.Equal(2, profiles.Count);
        Assert.All(profiles, p =>
        {
            Assert.NotNull(p.Intent);
            Assert.Equal("female", p.Intent!.Gender);
            Assert.Equal(70, p.Intent.MinAge);
        });
        Assert.NotSame(profiles[0].Intent, profiles[1].Intent);
        Assert.NotSame(profiles[0].Intent, intent);
    }
}
