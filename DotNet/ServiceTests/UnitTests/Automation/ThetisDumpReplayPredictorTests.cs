using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

/// <summary>
/// Replays Thetis-generated patient bundles from
/// <c>Documents/Automation Thetis Bundles</c> through
/// <see cref="AbsSubmissionPredictor"/> and compares clinical type counts to
/// the ABS actuals in that dump's RunManifest. Skips when the dump folder is
/// missing (CI) and skips patients that never submitted to ABS.
/// </summary>
[Trait("Category", "UnitTests")]
public class ThetisDumpReplayPredictorTests
{
    private static readonly string DumpRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Automation Thetis Bundles");

    private static readonly HashSet<string> SkipTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OperationOutcome",
        "Organization",
        "Device",
        "List"
    };

    [Fact]
    public void Submitted_thetis_dumps_predictor_matches_abs_actuals()
    {
        if (!Directory.Exists(DumpRoot))
            return;

        var failures = new List<string>();
        foreach (var folder in Directory.GetDirectories(DumpRoot).OrderBy(Path.GetFileName))
        {
            var name = Path.GetFileName(folder);
            if (name is null || !char.IsDigit(name[0]))
                continue;

            var genPath = Path.Combine(folder, "generation.json");
            var bundlePath = Path.Combine(folder, "patient-bundle.json");
            var manifestPath = Path.Combine(folder, "RunDiagnostics", "RunManifest.txt");
            if (!File.Exists(genPath) || !File.Exists(bundlePath) || !File.Exists(manifestPath))
                continue;

            var gen = JsonDocument.Parse(File.ReadAllText(genPath));
            var patientId = gen.RootElement.GetProperty("patientId").GetString()!;
            var manifestText = File.ReadAllText(manifestPath);
            if (!manifestText.Contains("submission=Submitted", StringComparison.Ordinal)
                && !manifestText.Contains($"{patientId}.ndjson  PRESENT", StringComparison.Ordinal))
            {
                continue;
            }

            var actuals = ParseAbsActuals(manifestText);
            if (actuals.Count == 0)
                continue;

            var measures = ParseMeasures(gen.RootElement);
            var periodStart = DateTime.Parse(gen.RootElement.GetProperty("periodStart").GetString()!).ToUniversalTime();
            var periodEnd = DateTime.Parse(gen.RootElement.GetProperty("periodEnd").GetString()!).ToUniversalTime();
            var bundleJson = File.ReadAllText(bundlePath);

            GenerationManifest predictedManifest;
            try
            {
                predictedManifest = AbsSubmissionPredictor.PredictImportedBundle(
                    bundleJson, patientId, periodStart, periodEnd, measures);
            }
            catch (Exception ex)
            {
                failures.Add($"{name}: predictor threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var predicted = predictedManifest.GetExpectedAbsCountsForPatient(patientId)
                            ?? new Dictionary<string, int>();
            var qualifies = predictedManifest.GetExpectedAbsKeysForPatient(patientId).Count > 0;
            if (!qualifies)
            {
                failures.Add($"{name}: predictor treated submitted patient as NQ (zero expected keys)");
                continue;
            }

            foreach (var (type, actual) in actuals.OrderBy(kv => kv.Key))
            {
                if (SkipTypes.Contains(type))
                    continue;
                predicted.TryGetValue(type, out var exp);
                if (exp != actual)
                    failures.Add($"{name} {type}: predicted {exp} ABS {actual} (delta {actual - exp:+0;-0})");
            }
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    private static List<ProfiledMeasureType> ParseMeasures(JsonElement gen)
    {
        var measures = new List<ProfiledMeasureType>();
        foreach (var m in gen.GetProperty("runMeasures").EnumerateArray())
        {
            if (Enum.TryParse<ProfiledMeasureType>(m.GetString(), out var parsed))
                measures.Add(parsed);
        }

        return measures.Count > 0
            ? measures
            : [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation];
    }

    private static Dictionary<string, int> ParseAbsActuals(string manifestText)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var inTable = false;
        foreach (var raw in manifestText.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Contains("EXPECTED vs ACTUAL (per resource type)", StringComparison.Ordinal))
            {
                inTable = true;
                continue;
            }

            if (!inTable)
                continue;
            if (line.Contains("Type", StringComparison.Ordinal) && line.Contains("Expected"))
                continue;
            if (line.TrimStart().StartsWith("----", StringComparison.Ordinal))
                continue;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("--", StringComparison.Ordinal))
                break;

            var match = Regex.Match(line, @"^\s+(\S+)\s+(\d+)\s+(\d+)\s+");
            if (!match.Success)
                continue;
            result[match.Groups[1].Value] = int.Parse(match.Groups[3].Value);
        }

        return result;
    }
}
