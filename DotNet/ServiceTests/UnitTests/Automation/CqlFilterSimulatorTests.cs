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

    // ---------- Procedure profile (ACH only) ----------

    private static CqlFilterSimulator.PatientCqlInput InputWithProcedures(params CqlFilterSimulator.ProcedureContext[] procedures)
        => new(
            PatientId: "P1",
            EncounterId: "E1",
            EncounterStart: EncStart,
            EncounterEnd: EncEnd,
            Conditions: Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Observations: Array.Empty<CqlFilterSimulator.ObservationContext>())
        { Procedures = procedures };

    [Fact]
    public void Ach_Procedure_PerformedOverlapsEncounter_IsKept()
    {
        var proc = new CqlFilterSimulator.ProcedureContext("P-001", EncStart.AddHours(1), EncStart.AddHours(2), $"Encounter/E1");

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation },
            InputWithProcedures(proc));

        Assert.DoesNotContain("Procedure/P-001", excluded);
    }

    [Fact]
    public void Ach_Procedure_PerformedOutsideEncounter_IsExcluded()
    {
        var proc = new CqlFilterSimulator.ProcedureContext("P-002", EncEnd.AddDays(2), EncEnd.AddDays(2).AddHours(1), $"Encounter/E1");

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation },
            InputWithProcedures(proc));

        Assert.Contains("Procedure/P-002", excluded);
    }

    [Fact]
    public void Hypoglycemic_Procedure_HasNoSdeRetrieve_DoesNotApply()
    {
        // Hypoglycemic doesn't retrieve Procedure via SDE. Per the per-type intersection
        // rule, when no profile applies to a resource type, no exclusion is contributed
        // (an empty bucket means the inner loop skips it).
        var proc = new CqlFilterSimulator.ProcedureContext("P-003", EncEnd.AddDays(2), EncEnd.AddDays(2).AddHours(1), $"Encounter/E1");

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation },
            InputWithProcedures(proc));

        Assert.DoesNotContain("Procedure/P-003", excluded);
    }

    // ---------- MedicationRequest profile (ACH + Hypo) ----------

    private static CqlFilterSimulator.PatientCqlInput InputWithMedicationRequests(params CqlFilterSimulator.MedicationRequestContext[] medReqs)
        => new(
            PatientId: "P1",
            EncounterId: "E1",
            EncounterStart: EncStart,
            EncounterEnd: EncEnd,
            Conditions: Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Observations: Array.Empty<CqlFilterSimulator.ObservationContext>())
        { MedicationRequests = medReqs };

    [Theory]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation)]
    public void MedicationRequest_AuthoredDuringEncounter_IsKept(ProfiledMeasureType measure)
    {
        var mr = new CqlFilterSimulator.MedicationRequestContext("MR-001", EncStart.AddHours(2), $"Encounter/E1");

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(new[] { measure }, InputWithMedicationRequests(mr));

        Assert.DoesNotContain("MedicationRequest/MR-001", excluded);
    }

    [Theory]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation)]
    public void MedicationRequest_AuthoredOutsideEncounter_IsExcluded(ProfiledMeasureType measure)
    {
        var mr = new CqlFilterSimulator.MedicationRequestContext("MR-002", EncEnd.AddDays(5), $"Encounter/E1");

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(new[] { measure }, InputWithMedicationRequests(mr));

        Assert.Contains("MedicationRequest/MR-002", excluded);
    }

    // ---------- MedicationAdministration profile (Hypo only) ----------

    [Fact]
    public void Hypoglycemic_MedicationAdministration_EffectiveOverlapsEncounter_IsKept()
    {
        var ma = new CqlFilterSimulator.MedicationAdministrationContext("MA-001", EncStart.AddHours(2), EncStart.AddHours(3), "Encounter/E1");

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E1", EncStart, EncEnd,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        { MedicationAdministrations = new[] { ma } };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation }, input);

        Assert.DoesNotContain("MedicationAdministration/MA-001", excluded);
    }

    [Fact]
    public void Hypoglycemic_MedicationAdministration_EffectiveOutsideEncounter_IsExcluded()
    {
        var ma = new CqlFilterSimulator.MedicationAdministrationContext("MA-002", EncEnd.AddDays(2), EncEnd.AddDays(2).AddHours(1), "Encounter/E1");

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E1", EncStart, EncEnd,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        { MedicationAdministrations = new[] { ma } };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation }, input);

        Assert.Contains("MedicationAdministration/MA-002", excluded);
    }

    [Fact]
    public void Ach_MedicationAdministration_HasNoSdeRetrieve_DoesNotApply()
    {
        var ma = new CqlFilterSimulator.MedicationAdministrationContext("MA-003", EncEnd.AddDays(2), EncEnd.AddDays(2).AddHours(1), "Encounter/E1");

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E1", EncStart, EncEnd,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        { MedicationAdministrations = new[] { ma } };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation }, input);

        Assert.DoesNotContain("MedicationAdministration/MA-003", excluded);
    }

    // ---------- Coverage profile ----------

    [Theory]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation)]
    public void Coverage_PeriodOverlapsEncounter_IsKept(ProfiledMeasureType measure)
    {
        // Coverage spans both before and after the encounter — clearly overlaps.
        var cov = new CqlFilterSimulator.CoverageContext("COV-001", EncStart.AddDays(-30), EncEnd.AddDays(180));

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E1", EncStart, EncEnd,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        { Coverages = new[] { cov } };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(new[] { measure }, input);

        Assert.DoesNotContain("Coverage/COV-001", excluded);
    }

    [Theory]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation)]
    public void Coverage_PeriodEntirelyBeforeEncounter_IsExcluded(ProfiledMeasureType measure)
    {
        var cov = new CqlFilterSimulator.CoverageContext("COV-002", EncStart.AddYears(-2), EncStart.AddYears(-1));

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E1", EncStart, EncEnd,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        { Coverages = new[] { cov } };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(new[] { measure }, input);

        Assert.Contains("Coverage/COV-002", excluded);
    }

    [Theory]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation)]
    public void Coverage_OpenEndedAfterEncounterStart_IsKept(ProfiledMeasureType measure)
    {
        // CQL semantics for Hypoglycemic explicitly handle null period.end as "still active".
        // Our overlap check honors it via the extractor clamping null end to MaxValue.
        var cov = new CqlFilterSimulator.CoverageContext("COV-003", EncStart.AddDays(-30), DateTime.MaxValue);

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E1", EncStart, EncEnd,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        { Coverages = new[] { cov } };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(new[] { measure }, input);

        Assert.DoesNotContain("Coverage/COV-003", excluded);
    }

    // ---------- ServiceRequest profile ----------

    [Theory]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation)]
    public void ServiceRequest_AuthoredDuringEncounter_IsKept(ProfiledMeasureType measure)
    {
        var sr = new CqlFilterSimulator.ServiceRequestContext("SR-001", EncStart.AddHours(3), $"Encounter/E1");

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E1", EncStart, EncEnd,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        { ServiceRequests = new[] { sr } };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(new[] { measure }, input);

        Assert.DoesNotContain("ServiceRequest/SR-001", excluded);
    }

    [Theory]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation)]
    public void ServiceRequest_AuthoredOutsideEncounter_IsExcluded(ProfiledMeasureType measure)
    {
        var sr = new CqlFilterSimulator.ServiceRequestContext("SR-002", EncEnd.AddDays(7), $"Encounter/E1");

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E1", EncStart, EncEnd,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        { ServiceRequests = new[] { sr } };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(new[] { measure }, input);

        Assert.Contains("ServiceRequest/SR-002", excluded);
    }

    // ---------- Multi-measure intersection across new types ----------

    [Fact]
    public void MedicationRequest_OutsideEncounter_BothMeasuresExclude_StillExcluded()
    {
        // Both ACH and Hypoglycemic apply the same predicate (authoredOn during IP). Both
        // exclude an out-of-window MedicationRequest, so the intersection (which keeps a key
        // only if every applicable profile excludes it) keeps it excluded.
        var mr = new CqlFilterSimulator.MedicationRequestContext("MR-multi-out", EncEnd.AddDays(10), $"Encounter/E1");

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[]
            {
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
            },
            InputWithMedicationRequests(mr));

        Assert.Contains("MedicationRequest/MR-multi-out", excluded);
    }

    // ---------- Specimen profiles ----------

    private static CqlFilterSimulator.SpecimenContext Specimen(
        string id,
        DateTime collectionStart,
        DateTime? collectionEnd = null,
        string patientId = "P1")
        => new(id, collectionStart, collectionEnd ?? collectionStart)
        {
            SubjectReference = $"Patient/{patientId}"
        };

    private static CqlFilterSimulator.ObservationContext LabObservationWithSpecimen(
        string id,
        string loinc,
        string specimenId)
        => new(
            ResourceId: id,
            LoincCode: loinc,
            CategoryCodes: new[] { "laboratory" },
            EffectiveStart: EncStart.AddHours(1),
            EffectiveEnd: EncStart.AddHours(2))
        {
            Status = "final",
            SpecimenReference = $"Specimen/{specimenId}"
        };

    private static CqlFilterSimulator.PatientCqlInput InputWithSpecimens(
        CqlFilterSimulator.SpecimenContext[] specimens,
        params CqlFilterSimulator.ObservationContext[] observations)
        => new(
            PatientId: "P1",
            EncounterId: "E1",
            EncounterStart: EncStart,
            EncounterEnd: EncEnd,
            Conditions: Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Observations: observations)
        { Specimens = specimens };

    [Fact]
    public void AchMonthly_Specimen_CollectedDuringEncounter_IsKept()
    {
        var specimen = Specimen("S-monthly-in", EncStart.AddHours(1));

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation },
            InputWithSpecimens(new[] { specimen }));

        Assert.DoesNotContain("Specimen/S-monthly-in", excluded);
    }

    [Fact]
    public void AchMonthly_Specimen_CollectedOutsideEncounter_IsExcluded()
    {
        var specimen = Specimen("S-monthly-out", EncEnd.AddDays(2));

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation },
            InputWithSpecimens(new[] { specimen }));

        Assert.Contains("Specimen/S-monthly-out", excluded);
    }

    [Fact]
    public void AchMonthly_Specimen_WithDifferentSubject_IsExcludedEvenWhenCollectedDuringEncounter()
    {
        var specimen = Specimen("S-monthly-other-patient", EncStart.AddHours(1), patientId: "OtherPatient");

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation },
            InputWithSpecimens(new[] { specimen }));

        Assert.Contains("Specimen/S-monthly-other-patient", excluded);
    }

    [Fact]
    public void AchDaily_Specimen_ReferencedOnlyByNonRespiratoryObservation_IsExcluded()
    {
        var specimen = Specimen("S-daily-non-rps", EncStart.AddHours(1));
        var sodium = LabObservationWithSpecimen("O-sodium", "2951-2", specimen.ResourceId);

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation },
            InputWithSpecimens(new[] { specimen }, sodium));

        Assert.Contains("Specimen/S-daily-non-rps", excluded);
    }

    [Fact]
    public void AchDaily_Specimen_ReferencedByRespiratoryPathogenObservation_IsKept()
    {
        var specimen = Specimen("S-daily-rps", EncStart.AddHours(1));
        var covid = LabObservationWithSpecimen("O-covid", "94500-6", specimen.ResourceId);

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation },
            InputWithSpecimens(new[] { specimen }, covid));

        Assert.DoesNotContain("Specimen/S-daily-rps", excluded);
    }

    [Fact]
    public void Hypoglycemic_Specimen_CollectedOutsideEncounter_IsExcluded()
    {
        var specimen = Specimen("S-hypo-out", EncEnd.AddDays(2));

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation },
            InputWithSpecimens(new[] { specimen }));

        Assert.Contains("Specimen/S-hypo-out", excluded);
    }

    [Fact]
    public void MixedMeasures_SpecimenKeptWhenAnyApplicableMeasureKeepsIt()
    {
        var specimen = Specimen("S-mixed", EncStart.AddHours(1));
        var sodium = LabObservationWithSpecimen("O-sodium", "2951-2", specimen.ResourceId);

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[]
            {
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation
            },
            InputWithSpecimens(new[] { specimen }, sodium));

        Assert.DoesNotContain("Specimen/S-mixed", excluded);
    }
}
