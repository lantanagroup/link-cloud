using System.Text.Json;
using FluentAssertions;
using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Helpers;

namespace UnitTests.Automation;

/// <summary>
/// Generates many patients across seeds and measures, then checks the CQL-dynamic
/// predictor against CQL-faithful invariants. Gold is the measure CQL, not the
/// predictor's previous output.
/// </summary>
[Trait("Category", "UnitTests")]
public class GeneratedPatientPredictorSeedSweepTests
{
    private static readonly DateTime PeriodStart = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc);

    private static readonly HashSet<string> MonthlyObservationCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "laboratory", "vital-signs", "imaging", "procedure"
    };

    private static readonly HashSet<string> HypoGlucoseLoincs = new(StringComparer.OrdinalIgnoreCase)
    {
        "2345-7", "2339-0", "41653-7", "10449-7", "10450-5"
    };

    private static readonly HashSet<string> GeneratorDiagnosticReportLoincs = new(StringComparer.OrdinalIgnoreCase)
    {
        "58410-2", "24323-8", "24331-1", "57698-3", "24357-6", "85319-2",
        "24627-2", "30954-2", "18726-0", "60567-5", "11524-6"
    };

    private static readonly int[] Seeds =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 17, 19, 23, 29, 31, 37,
        41, 42, 43, 47, 53, 67, 89, 99, 101, 111, 222, 256, 333, 444, 555, 666,
        777, 888, 999, 1000, 1024, 2026, 4096, 5000, 7777, 9999, 12345, 20240901
    ];

    private static readonly ProfiledMeasureType[] Measures =
    [
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
        ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation,
        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
    ];

    [Fact]
    public void Generated_patients_across_seeds_match_cql_invariants_per_measure()
    {
        var failures = new List<string>();
        var (ids, sharedEntries, practitionerIds, medicationIds) = FhirBundleGenerator.BuildSharedResources();
        var sharedSim = AbsSubmissionPredictor.IndexEntries(sharedEntries);
        var queryPlan = QueryPlanDefaults.GetDefaultAsInput();

        foreach (var seed in Seeds)
        {
            var patientId = ids.PatientId(0);
            List<Bundle.EntryComponent> entries;
            try
            {
                entries = FhirBundleGenerator.GeneratePatientEntries(
                    patientId,
                    ids,
                    practitionerIds,
                    medicationIds,
                    totalResourcesPerPatient: 220,
                    seed: seed,
                    clinicalPeriodStart: PeriodStart,
                    clinicalPeriodEnd: PeriodEnd);
            }
            catch (Exception ex)
            {
                failures.Add($"seed={seed} generate threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var cqlInput = CqlFilterInputExtractor.ExtractFromEntries(patientId, entries, sharedSim);
            if (cqlInput == null)
            {
                failures.Add($"seed={seed} extractor returned null (no encounter)");
                continue;
            }

            cqlInput = cqlInput with
            {
                MeasurementPeriodStart = PeriodStart,
                MeasurementPeriodEnd = PeriodEnd
            };

            foreach (var measure in Measures)
            {
                try
                {
                    AssertCqlInvariants(seed, measure, entries, cqlInput, failures);
                    AssertPredictorCompletes(seed, measure, patientId, entries, sharedSim, queryPlan, failures);
                }
                catch (Exception ex)
                {
                    failures.Add($"seed={seed} {ShortName(measure)} threw {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures.Take(40)));
    }

    [Fact]
    public void Same_generated_patient_predicted_counts_differ_by_measure_family()
    {
        var (ids, sharedEntries, practitionerIds, medicationIds) = FhirBundleGenerator.BuildSharedResources();
        var sharedSim = AbsSubmissionPredictor.IndexEntries(sharedEntries);
        var patientId = ids.PatientId(0);
        var entries = FhirBundleGenerator.GeneratePatientEntries(
            patientId, ids, practitionerIds, medicationIds,
            totalResourcesPerPatient: 400,
            seed: 42,
            clinicalPeriodStart: PeriodStart,
            clinicalPeriodEnd: PeriodEnd);

        var monthly = Predict(patientId, entries, sharedSim, ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation);
        var daily = Predict(patientId, entries, sharedSim, ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);
        var hypo = Predict(patientId, entries, sharedSim, ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        var monthlyObs = CountType(monthly, patientId, "Observation");
        var dailyObs = CountType(daily, patientId, "Observation");
        var hypoObs = CountType(hypo, patientId, "Observation");
        var monthlyDr = CountType(monthly, patientId, "DiagnosticReport");
        var dailyDr = CountType(daily, patientId, "DiagnosticReport");
        var hypoDr = CountType(hypo, patientId, "DiagnosticReport");

        dailyObs.Should().BeGreaterThanOrEqualTo(monthlyObs,
            "Daily SDE All Observations keeps categories Monthly drops (social-history/survey)");
        monthlyObs.Should().BeGreaterThan(hypoObs,
            "Hypo only keeps blood-glucose LOINCs; Monthly keeps lab/vitals/imaging/procedure");
        monthlyDr.Should().BeGreaterThan(dailyDr,
            "Daily has no all-DiagnosticReport SDE; generator panels are not COVID/flu/RSV LOINCs");
        dailyDr.Should().Be(0,
            "generated DiagnosticReport LOINCs are CBC/CMP/etc., not Daily COVID/flu/RSV valuesets");
        hypoDr.Should().Be(0,
            "Hypoglycemic CQL has no DiagnosticReport SDE retrieve");
    }

    private static void AssertCqlInvariants(
        int seed,
        ProfiledMeasureType measure,
        List<Bundle.EntryComponent> entries,
        CqlFilterSimulator.PatientCqlInput cqlInput,
        List<string> failures)
    {
        var excluded = CqlFilterSimulator.ComputeFilteredKeys([measure], cqlInput);
        var ip = cqlInput.Encounters.FirstOrDefault(e =>
            string.Equals(e.ClassCode, "IMP", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.ClassCode, "ACUTE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.ClassCode, "EMER", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.ClassCode, "OBSENC", StringComparison.OrdinalIgnoreCase));
        if (ip == null)
        {
            failures.Add($"seed={seed} {ShortName(measure)} no inpatient-class encounter");
            return;
        }

        foreach (var entry in entries)
        {
            switch (entry.Resource)
            {
                case Observation obs:
                    CheckObservation(seed, measure, obs, ip, excluded, failures);
                    break;
                case DiagnosticReport report:
                    CheckDiagnosticReport(seed, measure, report, ip, excluded, failures);
                    break;
                case Condition condition:
                    CheckCondition(seed, measure, condition, ip, excluded, failures);
                    break;
            }
        }
    }

    private static void CheckObservation(
        int seed,
        ProfiledMeasureType measure,
        Observation obs,
        CqlFilterSimulator.EncounterContext ip,
        HashSet<string> excluded,
        List<string> failures)
    {
        var key = $"Observation/{obs.Id}";
        var category = obs.Category?.SelectMany(c => c.Coding ?? [])
            .Select(c => c.Code)
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? string.Empty;
        var loinc = obs.Code?.Coding?.FirstOrDefault(c =>
            string.Equals(c.System, "http://loinc.org", StringComparison.OrdinalIgnoreCase))?.Code ?? string.Empty;
        var effective = ParseEffective(obs.Effective);
        var overlapsIp = Overlaps(effective.Start, effective.End, ip.PeriodStart, ip.PeriodEnd);
        var duringIp = During(effective.Start, effective.End, ip.PeriodStart, ip.PeriodEnd);

        var kept = !excluded.Contains(key);
        bool? shouldKeep = measure switch
        {
            ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation => true,
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation when
                MonthlyObservationCategories.Contains(category) => overlapsIp,
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation when
                category.Equals("social-history", StringComparison.OrdinalIgnoreCase)
                || category.Equals("survey", StringComparison.OrdinalIgnoreCase) => false,
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation when
                HypoGlucoseLoincs.Contains(loinc) && duringIp => true,
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation when
                !HypoGlucoseLoincs.Contains(loinc) => false,
            _ => null
        };

        if (shouldKeep is { } expected && kept != expected)
        {
            failures.Add(
                $"seed={seed} {ShortName(measure)} {key} cat={category} loinc={loinc} " +
                $"predictedKept={kept} cqlShouldKeep={expected}");
        }
    }

    private static void CheckDiagnosticReport(
        int seed,
        ProfiledMeasureType measure,
        DiagnosticReport report,
        CqlFilterSimulator.EncounterContext ip,
        HashSet<string> excluded,
        List<string> failures)
    {
        var key = $"DiagnosticReport/{report.Id}";
        var code = report.Code?.Coding?.FirstOrDefault()?.Code ?? string.Empty;
        var effective = ParseEffective(report.Effective);
        var overlapsIp = Overlaps(effective.Start, effective.End, ip.PeriodStart, ip.PeriodEnd);

        var shouldKeep = measure switch
        {
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => overlapsIp,
            ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation =>
                !GeneratorDiagnosticReportLoincs.Contains(code),
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation => true,
            _ => true
        };

        var kept = !excluded.Contains(key);
        if (measure == ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation)
        {
            if (excluded.Contains(key))
                failures.Add($"seed={seed} Hypo excluded {key} but CQL has no DiagnosticReport SDE");
            return;
        }

        if (kept != shouldKeep)
        {
            failures.Add(
                $"seed={seed} {ShortName(measure)} {key} code={code} " +
                $"predictedKept={kept} cqlShouldKeep={shouldKeep}");
        }
    }

    private static void CheckCondition(
        int seed,
        ProfiledMeasureType measure,
        Condition condition,
        CqlFilterSimulator.EncounterContext ip,
        HashSet<string> excluded,
        List<string> failures)
    {
        if (measure == ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)
            return;

        var key = $"Condition/{condition.Id}";
        var categories = (condition.Category ?? [])
            .SelectMany(c => c.Coding ?? [])
            .Select(c => c.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .ToList();
        var isEncounterDx = categories.Any(c =>
            c.Equals("encounter-diagnosis", StringComparison.OrdinalIgnoreCase));
        var encounterId = condition.Encounter?.Reference?.Split('/').LastOrDefault() ?? string.Empty;
        var listedOnIp = ip.DiagnosisConditionIds.Any(r =>
            string.Equals(r.Split('/').LastOrDefault(), condition.Id, StringComparison.OrdinalIgnoreCase));
        var linkedToIp = string.Equals(encounterId, ip.EncounterId, StringComparison.OrdinalIgnoreCase) || listedOnIp;
        var onset = ParseEffective(condition.Onset is Period or FhirDateTime ? condition.Onset : null);
        if (onset.Start == DateTime.MinValue)
            onset = (condition.RecordedDateElement != null
                ? ParseEffective(condition.RecordedDateElement)
                : (DateTime.MinValue, DateTime.MaxValue));

        var shouldKeep = measure switch
        {
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation =>
                isEncounterDx && linkedToIp,
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation =>
                isEncounterDx || Overlaps(onset.Start, onset.End, ip.PeriodStart, ip.PeriodEnd),
            _ => true
        };

        var kept = !excluded.Contains(key);
        if (kept != shouldKeep)
        {
            failures.Add(
                $"seed={seed} {ShortName(measure)} {key} cats=[{string.Join(',', categories)}] " +
                $"predictedKept={kept} cqlShouldKeep={shouldKeep}");
        }
    }

    private static void AssertPredictorCompletes(
        int seed,
        ProfiledMeasureType measure,
        string patientId,
        List<Bundle.EntryComponent> entries,
        List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)> sharedSim,
        QueryPlanInput queryPlan,
        List<string> failures)
    {
        var profile = new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>
        {
            [measure] = MeasureEligibility.Qualifying
        });
        var builder = new GenerationManifest.IncrementalBuilder();
        AbsSubmissionPredictor.PopulateManifest(
            builder,
            patientId,
            profile,
            entries,
            [measure],
            new FhirGenerationPipeline.AcquisitionSimulationConfig
            {
                QueryPlan = queryPlan,
                ClinicalPeriodStart = PeriodStart.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ClinicalPeriodEnd = PeriodEnd.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            PeriodStart,
            PeriodEnd,
            sharedSim,
            output: null);
        var manifest = builder.Build([measure]);
        manifest.AcquiredResourceTypes = QueryPlanDefaults.GetAcquiredResourceTypes(queryPlan);
        manifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures([measure]);
        var keys = manifest.GetExpectedAbsKeysForPatient(patientId);
        if (keys.Count == 0)
            failures.Add($"seed={seed} {ShortName(measure)} predicted zero ABS keys for a qualifying generated patient");
        if (!keys.Contains($"Patient/{patientId}"))
            failures.Add($"seed={seed} {ShortName(measure)} missing Patient/{patientId} in predicted ABS keys");
    }

    private static GenerationManifest Predict(
        string patientId,
        List<Bundle.EntryComponent> entries,
        List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)> sharedSim,
        ProfiledMeasureType measure)
    {
        var profile = new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>
        {
            [measure] = MeasureEligibility.Qualifying
        });
        var builder = new GenerationManifest.IncrementalBuilder();
        var queryPlan = QueryPlanDefaults.GetDefaultAsInput();
        AbsSubmissionPredictor.PopulateManifest(
            builder,
            patientId,
            profile,
            entries,
            [measure],
            new FhirGenerationPipeline.AcquisitionSimulationConfig
            {
                QueryPlan = queryPlan,
                ClinicalPeriodStart = PeriodStart.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ClinicalPeriodEnd = PeriodEnd.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            PeriodStart,
            PeriodEnd,
            sharedSim,
            output: null);
        var manifest = builder.Build([measure]);
        manifest.AcquiredResourceTypes = QueryPlanDefaults.GetAcquiredResourceTypes(queryPlan);
        manifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures([measure]);
        return manifest;
    }

    private static int CountType(GenerationManifest manifest, string patientId, string resourceType)
    {
        var counts = manifest.GetExpectedAbsCountsForPatient(patientId);
        if (counts == null)
            return 0;
        return counts.TryGetValue(resourceType, out var count) ? count : 0;
    }

    private static string ShortName(ProfiledMeasureType measure) => measure switch
    {
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => "Monthly",
        ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation => "Daily",
        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation => "Hypo",
        _ => measure.ToString()
    };

    private static bool Overlaps(DateTime start, DateTime end, DateTime ipStart, DateTime ipEnd)
        => start <= ipEnd && end >= ipStart;

    private static bool During(DateTime start, DateTime end, DateTime ipStart, DateTime ipEnd)
        => start >= ipStart && end <= ipEnd;

    private static (DateTime Start, DateTime End) ParseEffective(DataType? effective)
    {
        switch (effective)
        {
            case Period p:
                var start = DateTime.TryParse(p.Start, out var ps) ? ps.ToUniversalTime() : DateTime.MinValue;
                var end = DateTime.TryParse(p.End, out var pe) ? pe.ToUniversalTime() : start;
                return (start, end);
            case FhirDateTime dt:
                var instant = DateTime.TryParse(dt.Value, out var parsed) ? parsed.ToUniversalTime() : DateTime.MinValue;
                return (instant, instant);
            default:
                return (DateTime.MinValue, DateTime.MaxValue);
        }
    }
}
