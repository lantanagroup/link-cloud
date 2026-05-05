using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class MeasureInitialPopulationResolverTests
{
    private static readonly DateTime EncStart = new(2024, 1, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EncEnd = new(2024, 1, 12, 10, 0, 0, DateTimeKind.Utc);

    private static CqlFilterSimulator.EncounterContext Enc(
        string id,
        string classCode = "IMP",
        string status = "finished",
        DateTime? start = null,
        DateTime? end = null)
        => new(id, start ?? EncStart, end ?? EncEnd, classCode, status);

    private static CqlFilterSimulator.PatientCqlInput InputWithEncounters(params CqlFilterSimulator.EncounterContext[] encounters)
        => new(
            PatientId: "P1",
            EncounterId: encounters.Length > 0 ? encounters[0].EncounterId : "",
            EncounterStart: encounters.Length > 0 ? encounters[0].PeriodStart : DateTime.MinValue,
            EncounterEnd: encounters.Length > 0 ? encounters[0].PeriodEnd : DateTime.MaxValue,
            Conditions: Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Observations: Array.Empty<CqlFilterSimulator.ObservationContext>())
        { Encounters = encounters };

    // ---------- Class-code IP membership ----------

    [Theory]
    [InlineData("IMP", true)]
    [InlineData("ACUTE", true)]
    [InlineData("NONAC", true)]
    [InlineData("SS", true)]
    [InlineData("EMER", true)]
    [InlineData("OBSENC", true)]
    [InlineData("AMB", false)] // ambulatory is NOT in the ACH IP per CQL
    [InlineData("HH", false)]
    [InlineData("VR", false)]
    [InlineData("", false)]
    public void Ach_IpMembership_ByClassCode(string classCode, bool expectIp)
    {
        var input = InputWithEncounters(Enc("E1", classCode));

        var ipWindows = MeasureInitialPopulationResolver.Resolve(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation }, input);

        Assert.Equal(expectIp ? 1 : 0, ipWindows.Count);
    }

    [Theory]
    [InlineData("IMP", true)]
    [InlineData("ACUTE", true)]
    [InlineData("NONAC", true)]
    [InlineData("SS", true)]
    [InlineData("EMER", false)]
    [InlineData("OBSENC", false)]
    [InlineData("AMB", false)]
    public void Hypoglycemic_IpMembership_RequiresInpatientClass(string classCode, bool expectIp)
    {
        var input = InputWithEncounters(Enc("E1", classCode));

        var ipWindows = MeasureInitialPopulationResolver.Resolve(
            new[] { ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation }, input);

        Assert.Equal(expectIp ? 1 : 0, ipWindows.Count);
    }

    [Theory]
    [InlineData("finished", true)]
    [InlineData("in-progress", true)]
    [InlineData("triaged", true)]
    [InlineData("onleave", true)]
    [InlineData("entered-in-error", true)]
    [InlineData("planned", false)]
    [InlineData("cancelled", false)]
    [InlineData("arrived", false)]
    public void Ach_IpMembership_FiltersByEncounterStatus(string status, bool expectIp)
    {
        var input = InputWithEncounters(Enc("E1", "IMP", status: status));

        var ipWindows = MeasureInitialPopulationResolver.Resolve(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation }, input);

        Assert.Equal(expectIp ? 1 : 0, ipWindows.Count);
    }

    [Fact]
    public void Resolve_MultipleSelectedMeasures_UnionsIpEncounters()
    {
        // ACH would accept the EMER encounter; Hypo would only accept the IMP. The union
        // therefore contains both encounters.
        var inpatient = Enc("E-imp", "IMP");
        var emergency = Enc("E-emer", "EMER", start: EncEnd.AddDays(2), end: EncEnd.AddDays(2).AddHours(6));
        var input = InputWithEncounters(inpatient, emergency);

        var ipWindows = MeasureInitialPopulationResolver.Resolve(
            new[]
            {
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
            },
            input);

        Assert.Equal(2, ipWindows.Count);
        Assert.Contains(ipWindows, w => w.EncounterId == "E-imp");
        Assert.Contains(ipWindows, w => w.EncounterId == "E-emer");
    }

    [Fact]
    public void Resolve_DeduplicatesEncountersAcrossMeasures()
    {
        // The IMP encounter qualifies for both ACH and Hypo. The resolver should not
        // double-count it in the result.
        var inpatient = Enc("E-imp", "IMP");
        var input = InputWithEncounters(inpatient);

        var ipWindows = MeasureInitialPopulationResolver.Resolve(
            new[]
            {
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
            },
            input);

        Assert.Single(ipWindows);
    }

    [Fact]
    public void Resolve_EmptyEncountersList_ReturnsEmpty()
    {
        var input = InputWithEncounters();

        var ipWindows = MeasureInitialPopulationResolver.Resolve(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation }, input);

        Assert.Empty(ipWindows);
    }

    [Fact]
    public void Resolve_EncounterOutsideMeasurementPeriod_IsExcluded()
    {
        var encounter = Enc("E-out", "IMP",
            start: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2020, 1, 5, 0, 0, 0, DateTimeKind.Utc));

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", encounter.EncounterId, encounter.PeriodStart, encounter.PeriodEnd,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        {
            Encounters = new[] { encounter },
            MeasurementPeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            MeasurementPeriodEnd = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc)
        };

        var ipWindows = MeasureInitialPopulationResolver.Resolve(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation }, input);

        Assert.Empty(ipWindows);
    }

    [Fact]
    public void Resolve_DefaultOpenMeasurementPeriod_DoesNotFilterEncounters()
    {
        // When MeasurementPeriodStart/End are at default (MinValue/MaxValue), the resolver
        // accepts encounters regardless of date — preserves back-compat with extractor
        // outputs that don't set the measurement period.
        var encounter = Enc("E-old", "IMP",
            start: new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            end: new DateTime(1999, 1, 5, 0, 0, 0, DateTimeKind.Utc));
        var input = InputWithEncounters(encounter);

        var ipWindows = MeasureInitialPopulationResolver.Resolve(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation }, input);

        Assert.Single(ipWindows);
    }
}

[Trait("Category", "UnitTests")]
public class CqlFilterSimulatorMultiEncounterTests
{
    private static readonly DateTime Enc1Start = new(2024, 1, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Enc1End = new(2024, 1, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Enc2Start = new(2024, 4, 5, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Enc2End = new(2024, 4, 8, 10, 0, 0, DateTimeKind.Utc);

    private static CqlFilterSimulator.EncounterContext Enc(string id, string cls, DateTime start, DateTime end)
        => new(id, start, end, cls, "finished");

    [Fact]
    public void Procedure_OverlapsSecondEncounter_IsKept()
    {
        // Procedure performed during the second IP encounter should be kept by ACH even
        // though it's nowhere near the first encounter. With the legacy single-encounter
        // input this would have been incorrectly excluded.
        var enc1 = Enc("E1", "IMP", Enc1Start, Enc1End);
        var enc2 = Enc("E2", "IMP", Enc2Start, Enc2End);
        var procedure = new CqlFilterSimulator.ProcedureContext("P-001", Enc2Start.AddHours(1), Enc2Start.AddHours(2), "Encounter/E2");

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E1", Enc1Start, Enc1End,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        {
            Encounters = new[] { enc1, enc2 },
            Procedures = new[] { procedure }
        };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation }, input);

        Assert.DoesNotContain("Procedure/P-001", excluded);
    }

    [Fact]
    public void Encounter_NotOverlappingAnyIpWindow_IsExcluded()
    {
        // Two non-overlapping IP encounters; a third encounter with class IMP but in a
        // different time range that doesn't overlap either IP encounter should be
        // excluded by the Encounter profile.
        var ip1 = Enc("E-ip1", "IMP", Enc1Start, Enc1End);
        var ip2 = Enc("E-ip2", "IMP", Enc2Start, Enc2End);
        var stray = Enc("E-stray", "IMP",
            new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 8, 3, 0, 0, 0, DateTimeKind.Utc));

        // Note: stray HAS class IMP so it would also be IP-eligible. To exercise "not
        // overlapping any IP", give it a non-IP class so it isn't its own IP.
        stray = stray with { ClassCode = "VR" };

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E-ip1", Enc1Start, Enc1End,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        {
            Encounters = new[] { ip1, ip2, stray }
        };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation }, input);

        Assert.Contains("Encounter/E-stray", excluded);
        Assert.DoesNotContain("Encounter/E-ip1", excluded);
        Assert.DoesNotContain("Encounter/E-ip2", excluded);
    }

    [Fact]
    public void Encounter_NonIpButOverlapsIpWindow_IsKept()
    {
        // SDE Encounter semantics: a non-IP encounter (e.g. virtual visit) whose period
        // overlaps an IP encounter's period is pulled into ABS via the SDE retrieve.
        var ip = Enc("E-ip", "IMP", Enc1Start, Enc1End);
        // Virtual encounter overlapping the inpatient stay.
        var overlapping = Enc("E-vr", "VR", Enc1Start.AddHours(1), Enc1Start.AddHours(2));

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E-ip", Enc1Start, Enc1End,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        {
            Encounters = new[] { ip, overlapping }
        };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation }, input);

        Assert.DoesNotContain("Encounter/E-vr", excluded);
        Assert.DoesNotContain("Encounter/E-ip", excluded);
    }

    [Fact]
    public void Condition_EncounterDiagnosisMatchesSecondEncounter_IsKept()
    {
        // ACH SDE Condition: encounter-diagnosis tied to ANY IP encounter is kept. With
        // multi-encounter input the predicate tests against the IP set, not just the first.
        var enc1 = Enc("E1", "IMP", Enc1Start, Enc1End);
        var enc2 = Enc("E2", "IMP", Enc2Start, Enc2End);
        var condition = new CqlFilterSimulator.ConditionContext(
            ResourceId: "C-001",
            IsActive: true,
            RecordedDate: Enc2Start.Date,
            EncounterReference: "Encounter/E2",
            CategoryCodes: new[] { "encounter-diagnosis" });

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E1", Enc1Start, Enc1End,
            new[] { condition },
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        {
            Encounters = new[] { enc1, enc2 }
        };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation }, input);

        Assert.DoesNotContain("Condition/C-001", excluded);
    }

    [Fact]
    public void NoIpEncounters_AllResourcesExcluded()
    {
        // Patient has only VR (virtual) encounters — no IP for any selected measure.
        // Per CQL semantics, "where exists IP InitialPopulation where ..." returns nothing,
        // so every SDE retrieve produces nothing → every resource that the simulator's
        // applicable profiles would otherwise consider gets excluded.
        var virtual1 = Enc("E-vr", "VR", Enc1Start, Enc1End);
        var procedure = new CqlFilterSimulator.ProcedureContext("P-001", Enc1Start.AddHours(1), Enc1Start.AddHours(2), "Encounter/E-vr");

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E-vr", Enc1Start, Enc1End,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        {
            Encounters = new[] { virtual1 },
            Procedures = new[] { procedure }
        };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation }, input);

        Assert.Contains("Procedure/P-001", excluded);
        Assert.Contains("Encounter/E-vr", excluded);
    }

    // ---------- Back-compat: legacy single-encounter input still works ----------

    [Fact]
    public void LegacyInput_NoEncountersList_FallsBackToTriple()
    {
        // Pre-Landing-3 callers built PatientCqlInput with only the positional ctor and
        // didn't set Encounters. The simulator must still produce correct exclusions for
        // them, using EncounterId/Start/End as the single IP window.
        var procedure = new CqlFilterSimulator.ProcedureContext(
            "P-legacy", Enc1Start.AddHours(2), Enc1Start.AddHours(3), "Encounter/E1");

        var input = new CqlFilterSimulator.PatientCqlInput(
            "P1", "E1", Enc1Start, Enc1End,
            Array.Empty<CqlFilterSimulator.ConditionContext>(),
            Array.Empty<CqlFilterSimulator.ObservationContext>())
        { Procedures = new[] { procedure } };

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation }, input);

        Assert.DoesNotContain("Procedure/P-legacy", excluded);
    }
}
