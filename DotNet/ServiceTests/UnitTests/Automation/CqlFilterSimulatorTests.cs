using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class CqlFilterSimulatorTests
{
    private static readonly DateTime EncStart = new(2024, 1, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EncEnd = new(2024, 1, 12, 10, 0, 0, DateTimeKind.Utc);

    private static CqlFilterSimulator.PatientCqlInput InputWith(
        params CqlFilterSimulator.ObservationContext[] observations)
        => new(
            PatientId: "P1",
            EncounterId: "E1",
            EncounterStart: EncStart,
            EncounterEnd: EncEnd,
            Conditions: Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Observations: observations);

    private static CqlFilterSimulator.ObservationContext Obs(
        string id,
        string category,
        string loinc,
        DateTime effectiveStart,
        DateTime? effectiveEnd = null)
        => new(
            ResourceId: id,
            LoincCode: loinc,
            CategoryCodes: new[] { category },
            EffectiveStart: effectiveStart,
            EffectiveEnd: effectiveEnd ?? effectiveStart);

    // ---------- ACH (Monthly + Daily) ----------

    [Theory]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)]
    public void Ach_IncludesAllSupportedCategoriesWithinEncounter(ProfiledMeasureType measure)
    {
        var input = InputWith(
            Obs("o-lab", "laboratory", "718-7", EncStart.AddHours(1)),
            Obs("o-vitals", "vital-signs", "8867-4", EncStart.AddHours(2)),
            Obs("o-social", "social-history", "72166-2", EncStart.AddHours(3)),
            Obs("o-survey", "survey", "44249-1", EncStart.AddHours(4)));

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(new[] { measure }, input);

        Assert.Empty(excluded);
    }

    [Theory]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)]
    public void Ach_ExcludesUnsupportedCategoriesEvenWithinEncounter(ProfiledMeasureType measure)
    {
        var input = InputWith(
            Obs("o-exam", "exam", "8867-4", EncStart.AddHours(1)),
            Obs("o-therapy", "therapy", "8867-4", EncStart.AddHours(2)),
            Obs("o-activity", "activity", "8867-4", EncStart.AddHours(3)));

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(new[] { measure }, input);

        Assert.Equal(
            new HashSet<string> { "Observation/o-exam", "Observation/o-therapy", "Observation/o-activity" },
            excluded);
    }

    [Fact]
    public void Ach_ExcludesObservationsOutsideEncounterPeriod()
    {
        var input = InputWith(
            Obs("o-before", "laboratory", "718-7", EncStart.AddDays(-5)),
            Obs("o-after", "laboratory", "718-7", EncEnd.AddDays(5)),
            Obs("o-during", "laboratory", "718-7", EncStart.AddHours(1)));

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation },
            input);

        Assert.Contains("Observation/o-before", excluded);
        Assert.Contains("Observation/o-after", excluded);
        Assert.DoesNotContain("Observation/o-during", excluded);
    }

    [Fact]
    public void Ach_PeriodEffective_IncludesIfPeriodOverlapsEncounter()
    {
        // Period straddles the encounter start - should be considered overlapping.
        var input = InputWith(
            Obs("o-overlap-start", "laboratory", "718-7",
                effectiveStart: EncStart.AddHours(-3),
                effectiveEnd: EncStart.AddHours(1)),
            // Period strictly after encounter end - excluded.
            Obs("o-after-period", "laboratory", "718-7",
                effectiveStart: EncEnd.AddHours(2),
                effectiveEnd: EncEnd.AddHours(5)));

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation },
            input);

        Assert.DoesNotContain("Observation/o-overlap-start", excluded);
        Assert.Contains("Observation/o-after-period", excluded);
    }

    // ---------- Hypoglycemic ----------

    [Fact]
    public void Hypoglycemic_IncludesOnlyBloodGlucoseLoincsWithinEncounter()
    {
        var input = InputWith(
            Obs("o-glu-serum", "laboratory", "2345-7", EncStart.AddHours(1)),
            Obs("o-glu-blood", "laboratory", "2339-0", EncStart.AddHours(2)),
            Obs("o-glu-poc", "laboratory", "41653-7", EncStart.AddHours(3)),
            Obs("o-na", "laboratory", "2951-2", EncStart.AddHours(4)),
            Obs("o-vitals", "vital-signs", "8867-4", EncStart.AddHours(5)));

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation },
            input);

        Assert.DoesNotContain("Observation/o-glu-serum", excluded);
        Assert.DoesNotContain("Observation/o-glu-blood", excluded);
        Assert.DoesNotContain("Observation/o-glu-poc", excluded);
        Assert.Contains("Observation/o-na", excluded);
        Assert.Contains("Observation/o-vitals", excluded);
    }

    [Fact]
    public void Hypoglycemic_ExcludesGlucoseOutsideInitialPopulation()
    {
        var input = InputWith(
            Obs("o-glu-before", "laboratory", "2345-7", EncStart.AddDays(-1)),
            Obs("o-glu-after", "laboratory", "2345-7", EncEnd.AddDays(1)));

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation },
            input);

        Assert.Contains("Observation/o-glu-before", excluded);
        Assert.Contains("Observation/o-glu-after", excluded);
    }

    // ---------- Mixed-measure intersection ----------

    [Fact]
    public void MixedMeasures_OnlyExcludesWhenEveryApplicableProfileExcludes()
    {
        // ACH would include the vital-sign observation (vital-signs in IP),
        // but Hypoglycemic excludes everything that isn't a glucose LOINC.
        // The intersection rule means the vital-sign should NOT appear in the
        // excluded set, because at least one applicable measure (ACH) keeps it.
        var input = InputWith(
            Obs("o-vitals", "vital-signs", "8867-4", EncStart.AddHours(1)));

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[]
            {
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
            },
            input);

        Assert.DoesNotContain("Observation/o-vitals", excluded);
    }

    [Fact]
    public void MixedMeasures_ExcludesObservationDroppedByAllApplicableProfiles()
    {
        // An "exam" category observation is dropped by ACH (unsupported category)
        // and by Hypoglycemic (not a glucose LOINC). All applicable profiles
        // exclude it, so the intersection includes it.
        var input = InputWith(
            Obs("o-exam", "exam", "0000-0", EncStart.AddHours(1)));

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[]
            {
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
            },
            input);

        Assert.Contains("Observation/o-exam", excluded);
    }

    // ---------- Per-resource-type intersection regression ----------

    [Fact]
    public void PerTypeIntersection_ObservationProfileDoesNotErase_ConditionExclusions()
    {
        // Regression guard: a profile that targets Observations must not be intersected
        // against a profile that targets Conditions. If the simulator intersected globally,
        // every Condition exclusion would be wiped out the moment any Observation profile
        // was applicable, because the two profiles never produce overlapping keys.
        var encStart = new DateTime(2024, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        var encEnd = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc);
        var encounterId = "P-Enc-001";

        // Condition that ACH excludes: resolved problem-list-item with recordedDate
        // not strictly before encounter end.
        var excludedCondition = new CqlFilterSimulator.ConditionContext(
            ResourceId: "P-Condition-001",
            IsActive: false,
            RecordedDate: encEnd.Date,
            EncounterReference: $"Encounter/{encounterId}",
            CategoryCodes: new[] { "problem-list-item" });

        // Observation ACH would keep (lab inside the encounter).
        var keptObservation = Obs("o-lab", "laboratory", "718-7", encStart.AddHours(1));

        var input = new CqlFilterSimulator.PatientCqlInput(
            PatientId: "P",
            EncounterId: encounterId,
            EncounterStart: encStart,
            EncounterEnd: encEnd,
            Conditions: new[] { excludedCondition },
            Observations: new[] { keptObservation });

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation },
            input);

        Assert.Contains("Condition/P-Condition-001", excluded);
        Assert.DoesNotContain("Observation/o-lab", excluded);
    }
}
