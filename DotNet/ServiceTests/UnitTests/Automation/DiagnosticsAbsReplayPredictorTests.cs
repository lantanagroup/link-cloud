using System.Text;
using System.Text.Json;
using FluentAssertions;
using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

/// <summary>
/// Replays Run Diagnostics ABS artifacts through <see cref="AbsSubmissionPredictor"/>
/// and compares predicted type counts to the ABS actuals from that run.
///
/// Pattern when a predictor miss shows up on a seed/config/imported bundle:
/// capture the patient ABS ndjson (and any missing expected keys), commit them
/// here, and assert <see cref="GenerationManifest.GetExpectedAbsCountsForPatient"/>
/// matches the diagnostics ABS counts.
/// </summary>
[Trait("Category", "UnitTests")]
public class DiagnosticsAbsReplayPredictorTests
{
    private static readonly HashSet<string> SkipAbsReplayTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "MeasureReport",
        "OperationOutcome",
        "Organization",
        "Device",
        "List"
    };

    private static readonly HashSet<string> PipelineDerivedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "MeasureReport",
        "OperationOutcome",
        "Organization"
    };

    [Fact]
    public void Run1_imported_patient_predictor_matches_abs_type_counts()
    {
        // Run dc58502b: imported 019eb19d. ABS had 249 Observations including 14 imaging.
        // Feeding the ABS clinical artifact back through the predictor must not
        // drop imaging (the greedy Category ~ span bug).
        const string patientId = "019eb19d-249b-7ea8-8ddf-0e82340c1776";
        var bundleJson = WrapClinicalNdjsonAsBundle(ReadEmbedded("run1-imported-019eb19d.abs.ndjson"));
        var periodStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);

        var manifest = AbsSubmissionPredictor.PredictImportedBundle(
            bundleJson, patientId, periodStart, periodEnd);
        var predicted = ClinicalCounts(manifest, patientId);

        AssertCount(predicted, "Observation", 249);
        AssertCount(predicted, "DiagnosticReport", 18);
        AssertCount(predicted, "Encounter", 7);
        AssertCount(predicted, "Location", 6);
        AssertCount(predicted, "Condition", 3);
        AssertCount(predicted, "Coverage", 1);
        AssertCount(predicted, "Medication", 4);
        AssertCount(predicted, "MedicationRequest", 4);
        AssertCount(predicted, "ServiceRequest", 21);
        AssertCount(predicted, "Specimen", 15);
        AssertCount(predicted, "Patient", 1);

        manifest.GetExpectedAbsKeysForPatient(patientId)
            .Should()
            .Contain("Observation/019eb19d-26fa-78c4-abe3-42ec76dae3e7",
                "imaging Observation that landed in ABS must stay in the predicted set");
    }

    [Fact]
    public void Run2_mega_patient_predictor_excludes_out_of_ip_diagnostic_reports()
    {
        // Run df6f9b8e: mega AddById patient. DA acquired 360 DiagnosticReports;
        // ABS kept 255 whose effective overlapped the IMP encounter (Jul 9-25).
        // Reconstruct the acquired set by adding the 105 missing DR ids dated in
        // the report period but outside the IP window.
        const string patientId = "Mpkn4SO0V9DoLQiKgv181TGGn7Lsjrio9f5ibdJizVK8o";
        var bundleJson = WrapClinicalNdjsonAsBundle(ReadEmbedded("run2-mega-Mpkn4SO0.abs.ndjson"));
        var entries = ImportedPatientLoader.ParseBundleEntries(bundleJson, patientId);
        AppendOutOfIpDiagnosticReports(entries, ReadEmbeddedJsonArray("run2-missing-diagnosticreport-ids.json"));

        var periodStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 8, 31, 23, 59, 59, DateTimeKind.Utc);
        var measures = new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation };
        var queryPlan = QueryPlanDefaults.GetDefaultAsInput();
        var acquisition = new FhirGenerationPipeline.AcquisitionSimulationConfig
        {
            QueryPlan = queryPlan,
            ClinicalPeriodStart = "2026-07-01T00:00:00Z",
            ClinicalPeriodEnd = "2026-08-31T23:59:59Z"
        };

        var builder = new GenerationManifest.IncrementalBuilder();
        var profile = new PatientProfile(
            measures.ToDictionary(m => m, _ => MeasureEligibility.Qualifying));
        AbsSubmissionPredictor.PopulateManifest(
            builder,
            patientId,
            profile,
            entries,
            measures,
            acquisition,
            periodStart,
            periodEnd,
            sharedSimEntries: null,
            output: null);

        var manifest = builder.Build(measures);
        manifest.AcquiredResourceTypes = QueryPlanDefaults.GetAcquiredResourceTypes(queryPlan);
        manifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures(measures);
        var predicted = ClinicalCounts(manifest, patientId);

        AssertCount(predicted, "DiagnosticReport", 255);
        AssertCount(predicted, "Observation", 24);
        AssertCount(predicted, "Condition", 11);
        AssertCount(predicted, "Encounter", 1);
        AssertCount(predicted, "Location", 1);
        AssertCount(predicted, "Medication", 8);
        AssertCount(predicted, "MedicationRequest", 8);
        AssertCount(predicted, "ServiceRequest", 84);
        AssertCount(predicted, "Procedure", 1);
        AssertCount(predicted, "Patient", 1);
    }

    private static void AssertCount(Dictionary<string, int> predicted, string resourceType, int expected)
    {
        predicted.Should().ContainKey(resourceType);
        predicted[resourceType].Should().Be(expected, $"ABS actual count for {resourceType}");
    }

    private static Dictionary<string, int> ClinicalCounts(GenerationManifest manifest, string patientId)
    {
        var counts = manifest.GetExpectedAbsCountsForPatient(patientId)
                     ?? throw new InvalidOperationException($"No ABS prediction for {patientId}.");
        return counts
            .Where(kv => !PipelineDerivedTypes.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static string WrapClinicalNdjsonAsBundle(string ndjson)
    {
        var parts = new List<string>();
        using var reader = new StringReader(ndjson);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            using var doc = JsonDocument.Parse(trimmed);
            var type = doc.RootElement.TryGetProperty("resourceType", out var rt)
                ? rt.GetString()
                : null;
            if (string.IsNullOrEmpty(type) || SkipAbsReplayTypes.Contains(type))
                continue;

            parts.Add("{\"resource\":" + trimmed + "}");
        }

        return "{\"resourceType\":\"Bundle\",\"type\":\"collection\",\"entry\":[" +
               string.Join(",", parts) + "]}";
    }

    private static void AppendOutOfIpDiagnosticReports(
        List<Bundle.EntryComponent> entries,
        IReadOnlyList<string> missingIds)
    {
        var template = entries.Select(e => e.Resource).OfType<DiagnosticReport>().First();
        foreach (var id in missingIds)
        {
            var report = new DiagnosticReport
            {
                Id = id,
                Status = DiagnosticReport.DiagnosticReportStatus.Final,
                Code = template.Code,
                Subject = template.Subject,
                Effective = new FhirDateTime("2026-08-15T12:00:00Z")
            };
            entries.Add(new Bundle.EntryComponent
            {
                Resource = report,
                Request = new Bundle.RequestComponent
                {
                    Method = Bundle.HTTPVerb.PUT,
                    Url = $"DiagnosticReport/{id}"
                }
            });
        }
    }

    private static string ReadEmbedded(string fileName)
    {
        var assembly = typeof(DiagnosticsAbsReplayPredictorTests).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new FileNotFoundException(resourceName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static List<string> ReadEmbeddedJsonArray(string fileName)
        => JsonSerializer.Deserialize<List<string>>(ReadEmbedded(fileName))
           ?? throw new InvalidOperationException(fileName);
}
