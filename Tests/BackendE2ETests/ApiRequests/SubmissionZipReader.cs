using LantanaGroup.Link.Tests.E2ETests;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;
using static LantanaGroup.Link.Tests.E2ETests.TestConfig.ValidationHelper;

namespace LantanaGroup.Link.Tests.BackendE2ETests.ApiRequests;

public class SubmissionZipReader(ITestOutputHelper output)
{
    protected static readonly string api_LinkAdminBffURL = TestConfig.AdminBffBase;
    protected static readonly string fhirServerBaseUrl = TestConfig.InternalFhirServerBase;
    protected static readonly string SingleMeasureAdHocFacility = TestConfig.SingleMeasureAdHocFacility;
    protected static readonly string SingleMeasureAdHocAchDqmVersion = TestConfig.SingleMeasureAdHocAchDqmVersion;
    protected static readonly string[] SingleMeasureExpectedFiles = TestConfig.SingleMeasureExpectedFiles;
    protected static readonly string[] SingleMeasureExpectedPatientIDs = TestConfig.SingleMeasureExpectedPatientIds;
    private readonly HttpClient _client = new HttpClient();
    private readonly Dictionary<string, string> _zipContents = new();
    string AdHocReportGuid => TestConfig.TestContextStore.AdHocReportTrackingIdGuid;

    public async Task DownloadAndExtractSingleMeasureZipAsync(bool save = false)
    {

        if (string.IsNullOrEmpty(SingleMeasureAdHocFacility))
            throw new InvalidOperationException("Facility ID must be set using UseSingleMeasureFacility() or UseMultiMeasureFacility().");

        var url = $"{api_LinkAdminBffURL}/Submission/{SingleMeasureAdHocFacility}/{AdHocReportGuid}";
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        byte[] zipBytes = await response.Content.ReadAsByteArrayAsync();
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        if (save && !string.IsNullOrEmpty(TestConfig.SmokeTestDownloadPath))
        {
            if (!Directory.Exists(TestConfig.SmokeTestDownloadPath))
                Directory.CreateDirectory(TestConfig.SmokeTestDownloadPath);

            var downloadPath = Path.Combine(TestConfig.SmokeTestDownloadPath, "adhoc-reporting-smoke-test-submission.zip");
            await File.WriteAllBytesAsync(downloadPath, zipBytes);
            output.WriteLine($"Report downloaded to {downloadPath}");
        }

        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase))
                continue;

            using var reader = new StreamReader(entry.Open());
            string content = await reader.ReadToEndAsync();
            _zipContents[entry.FullName] = content;
        }
    }
    public void SingleMeasureAdHocValidateFilesAppear()
    {
        var missingFiles = SingleMeasureExpectedFiles
            .Where(expected => !_zipContents.Keys.Any(actual =>
                actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missingFiles.Any())
        {
            foreach (var file in missingFiles)
                output.WriteLine($"🔴  [ERROR] {file} is missing.");

            string fileList = string.Join(", ", missingFiles);
            throw new Exception($"Verification failed: {missingFiles.Count} file(s) missing: {fileList}");
        }

        output.WriteLine("[PASS] All expected files appear in the ZIP archive.");
    }
    public void SingleMeasureAdHocValidateFilesDoNotAppear()
    {
        if(_zipContents.Count == 0)
            throw new InvalidOperationException("[FAIL] SingleMeasureAdHocValidateFilesDoNotAppear(): ZIP contents have not been loaded.");

        var disallowedFiles = new List<string>
        {
            TestConfig.singleMeasureAdHocTestPatient_Seven,
            TestConfig.singleMeasureAdHocTestPatient_Eight,
            TestConfig.singleMeasureAdHocTestPatient_Nine,
            TestConfig.singleMeasureAdHocTestPatient_Ten,
        };

        var foundDisallowedFiles = disallowedFiles
            .Where(disallowed => _zipContents.Keys.Any(actual => actual.EndsWith(disallowed, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (foundDisallowedFiles.Any())
        {
            foreach (var file in foundDisallowedFiles)
                output.WriteLine($"🔴  [ERROR] {file} was found but should NOT be present.");

            throw new Exception($"Verification failed: {foundDisallowedFiles.Count} disallowed file(s) were found.");
        }
        output.WriteLine("[PASS] No disallowed files were found in the ZIP archive.");
    }
    public void ValidatePatientFile_1(List<ValidationIssue> issues)
    {
        const string fileName = TestConfig.singleMeasureAdHocTestPatient_One;
        var expectedBundleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServiceRequest"] = 20,
            ["Observation"] = 11,
            ["Location"] = 1,
            ["Condition"] = 2,
            ["Medication"] = 2,
            ["MedicationRequest"] = 2,
            ["Patient"] = 1,
            ["Encounter"] = 1,
            ["Coverage"] = 1,
            ["Procedure"] = 1,
            ["DiagnosticReport"] = 1,
            ["OperationOutcome"] = 1,
            ["MeasureReport"] = 1
        };

        var expectedEvalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["__TOTAL__"] = 43,  

            ["ServiceRequest"] = 20,
            ["Observation"] = 11,
            ["Location"] = 1,
            ["Condition"] = 2,
            ["Medication"] = 2,
            ["MedicationRequest"] = 2,
            ["Patient"] = 1,
            ["Encounter"] = 1,
            ["Coverage"] = 1,
            ["Procedure"] = 1,
            ["DiagnosticReport"] = 1
        };

        ValidateSinglePatientFile(
            fileName,
            expectedBundleCounts,
            expectedEvalCounts, 
            issues);
    }
    public void ValidatePatientFile_2(List<ValidationIssue> issues)
    {
        const string fileName = TestConfig.singleMeasureAdHocTestPatient_Two;
        var expectedBundleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServiceRequest"] = 126,
            ["Observation"] = 126,
            ["MedicationRequest"] = 4,
            ["Medication"] = 4,
            ["Condition"] = 4,
            ["Location"] = 2,
            ["Encounter"] = 2,
            ["Coverage"] = 2,
            ["Patient"] = 1,
            ["OperationOutcome"] = 1,
            ["MeasureReport"] = 1
        };

        var expectedEvalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["__TOTAL__"] = 271,

            ["ServiceRequest"] = 126,
            ["Observation"] = 126,
            ["MedicationRequest"] = 4,
            ["Medication"] = 4,
            ["Condition"] = 4,
            ["Location"] = 2,
            ["Encounter"] = 2,
            ["Coverage"] = 2,
            ["Patient"] = 1
        };

        ValidateSinglePatientFile(fileName, expectedBundleCounts, expectedEvalCounts, issues);
    }
    public void ValidatePatientFile_3(List<ValidationIssue> issues)
    {
        const string fileName = TestConfig.singleMeasureAdHocTestPatient_Three;
        var expectedBundleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServiceRequest"] = 16,
            ["Observation"] = 5,
            ["Location"] = 1,
            ["Condition"] = 2,
            ["Medication"] = 1,
            ["MedicationRequest"] = 1,
            ["Procedure"] = 2,
            ["Patient"] = 1,
            ["Encounter"] = 1,
            ["Coverage"] = 1,
            ["Device"] = 1,
            ["DiagnosticReport"] = 1,
            ["OperationOutcome"] = 1,
            ["MeasureReport"] = 1
        };

        var expectedEvalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["__TOTAL__"] = 33,

            ["ServiceRequest"] = 16,
            ["Observation"] = 5,
            ["Location"] = 1,
            ["Condition"] = 2,
            ["Medication"] = 1,
            ["MedicationRequest"] = 1,
            ["Procedure"] = 2,
            ["Patient"] = 1,
            ["Encounter"] = 1,
            ["Coverage"] = 1,
            ["Device"] = 1,
            ["DiagnosticReport"] = 1
        };

        ValidateSinglePatientFile(
            fileName,
            expectedBundleCounts,
            expectedEvalCounts,
            issues);
    }
    public void ValidatePatientFile_4(List<ValidationIssue> issues)
    {
        const string fileName = TestConfig.singleMeasureAdHocTestPatient_Four;
        var expectedBundleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServiceRequest"] = 27,
            ["Observation"] = 9,
            ["Location"] = 1,
            ["Condition"] = 2,
            ["Medication"] = 2,
            ["MedicationRequest"] = 2,
            ["Procedure"] = 4,
            ["Encounter"] = 2,
            ["Coverage"] = 2,
            ["Device"] = 2,
            ["DiagnosticReport"] = 2,
            ["Patient"] = 1,
            ["OperationOutcome"] = 1,
            ["MeasureReport"] = 1
        };

        var expectedEvalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["__TOTAL__"] = 56, 

            ["ServiceRequest"] = 27,
            ["Observation"] = 9,
            ["Location"] = 1,
            ["Condition"] = 2,
            ["Medication"] = 2,
            ["MedicationRequest"] = 2,
            ["Procedure"] = 4,
            ["Encounter"] = 2,
            ["Coverage"] = 2,
            ["Device"] = 2,
            ["DiagnosticReport"] = 2,
            ["Patient"] = 1
        };

        ValidateSinglePatientFile(
            fileName,
            expectedBundleCounts,
            expectedEvalCounts,
            issues);
    }
    public void ValidatePatientFile_5(List<ValidationIssue> issues)
    {
        const string fileName = TestConfig.singleMeasureAdHocTestPatient_Five;
        var expectedBundleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServiceRequest"] = 31,
            ["Observation"] = 11,
            ["Location"] = 1,
            ["Condition"] = 2,
            ["Medication"] = 2,
            ["MedicationRequest"] = 2,
            ["Patient"] = 1,
            ["Encounter"] = 1,
            ["Coverage"] = 1,
            ["Procedure"] = 1,
            ["DiagnosticReport"] = 1,
            ["OperationOutcome"] = 1,
            ["MeasureReport"] = 1
        };

        var expectedEvalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["__TOTAL__"] = 54, 

            ["ServiceRequest"] = 31,
            ["Observation"] = 11,
            ["Location"] = 1,
            ["Condition"] = 2,
            ["Medication"] = 2,
            ["MedicationRequest"] = 2,
            ["Patient"] = 1,
            ["Encounter"] = 1,
            ["Coverage"] = 1,
            ["Procedure"] = 1,
            ["DiagnosticReport"] = 1
        };

        ValidateSinglePatientFile(
            fileName,
            expectedBundleCounts,
            expectedEvalCounts,
            issues);
    }
    public void ValidatePatientFile_6(List<ValidationIssue> issues)
    {
        const string fileName = TestConfig.singleMeasureAdHocTestPatient_Six;
        var expectedBundleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServiceRequest"] = 116,
            ["Observation"] = 23,
            ["Procedure"] = 3,
            ["Condition"] = 4,
            ["MedicationRequest"] = 4,
            ["Medication"] = 4,
            ["DiagnosticReport"] = 2,
            ["Encounter"] = 2,
            ["Coverage"] = 2,
            ["Device"] = 1,
            ["Location"] = 2,
            ["Patient"] = 1,
            ["OperationOutcome"] = 1,
            ["MeasureReport"] = 1
        };

        var expectedEvalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["__TOTAL__"] = 164,

            ["ServiceRequest"] = 116,
            ["Observation"] = 23,
            ["Procedure"] = 3,
            ["Condition"] = 4,
            ["MedicationRequest"] = 4,
            ["Medication"] = 4,
            ["DiagnosticReport"] = 2,
            ["Encounter"] = 2,
            ["Coverage"] = 2,
            ["Device"] = 1,
            ["Location"] = 2,
            ["Patient"] = 1
        };

        ValidateSinglePatientFile(
            fileName,
            expectedBundleCounts,
            expectedEvalCounts,
            issues);
    }

    private void ValidateSinglePatientFile(
    string fileName,
    Dictionary<string, int> expectedBundleCounts,
    Dictionary<string, int> expectedEvalCounts,
    List<ValidationIssue> issues)
    {
        var excludedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "OperationOutcome",
        "MeasureReport"
    };

        var entryKey = _zipContents.Keys
            .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (entryKey == null)
        {
            ReportIssue(
                fileName,
                resourceType: "",
                issueType: ValidationIssueType.MissingResourceTypeInBundle,
                message: $"{fileName} is missing from the ZIP archive.",
                issues: issues);
            return;
        }

        var content = _zipContents[entryKey];
        if (string.IsNullOrWhiteSpace(content))
        {
            ReportIssue(
                fileName,
                resourceType: "",
                issueType: ValidationIssueType.MissingResourceTypeInBundle,
                message: $"{fileName} is empty.",
                issues: issues);
            return;
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var lines = content
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line, jsonOptions))
            .ToList();

        if (lines.Count == 0)
        {
            ReportIssue(
                fileName,
                resourceType: "",
                issueType: ValidationIssueType.MissingResourceTypeInBundle,
                message: $"{fileName} contained only whitespace.",
                issues: issues);
            return;
        }

        var bundleCounts = CountResourceTypes(lines);
        var bundleIdsByType = BundleIdsByType(lines);

        var (evalCounts, evalIdsByType, crossTypeRefs) =
            ParseEvaluatedResource(lines, bundleIdsByType, excludedTypes);
        var rawEvalTypes = GetEvaluatedTypesRaw(lines);


        foreach (var kv in expectedBundleCounts)
        {
            var type = kv.Key;
            var expected = kv.Value;

            var exists = bundleCounts.TryGetValue(type, out var actual);

            if (!exists && expected > 0)
            {
                ReportIssue(fileName, type,
                    ValidationIssueType.MissingResourceTypeInBundle,
                    $"Missing required resourceType '{type}' (expected {expected}, got 0).",
                    issues);
                continue;
            }

            if (actual != expected)
            {
                ReportIssue(fileName, type,
                    ValidationIssueType.BundleCountMismatch,
                    $"Bundle count mismatch for '{type}': expected {expected}, got {actual}.",
                    issues);
            }
        }

        foreach (var kv in expectedEvalCounts)
        {
            var type = kv.Key;
            var expected = kv.Value;
            var actual = evalCounts.TryGetValue(type, out var n) ? n : 0;

            if (actual != expected)
            {
                var foundPretty = string.Join(", ",
                    evalCounts.Where(kv2 => !kv2.Key.Equals("__TOTAL__", StringComparison.OrdinalIgnoreCase))
                              .OrderBy(kv2 => kv2.Key, StringComparer.OrdinalIgnoreCase)
                              .Select(kv2 => $"{kv2.Key}={kv2.Value}"));

                var totalFound = evalCounts.TryGetValue("__TOTAL__", out var t) ? t : 0;

                string msg;
                if (string.Equals(type, "__TOTAL__", StringComparison.OrdinalIgnoreCase))
                {
                    msg =
                        $"evaluatedResource mismatch for '__TOTAL__': expected {expected}, got {actual}. " +
                        $"Breakdown: [{foundPretty}]";
                }
                else
                {
                    msg =
                        $"evaluatedResource mismatch for '{type}': expected {expected}, got {actual}.";
                }

                ReportIssue(
                    fileName,
                    resourceType: type,
                    issueType: ValidationIssueType.EvaluatedCountMismatch,
                    message: msg,
                    issues: issues);
            }
        }

        var expectedEvalKeys = new HashSet<string>(expectedEvalCounts.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var extraKey in evalCounts.Keys.Where(k => !expectedEvalKeys.Contains(k)))
        {
            var issueType =
                string.Equals(extraKey, "OperationOutcome", StringComparison.OrdinalIgnoreCase)
                    ? ValidationIssueType.ObservedResourceTypeCount
                    : ValidationIssueType.UnexpectedEvaluatedType;

            ReportIssue(
                fileName,
                resourceType: extraKey,
                issueType: issueType,
                message: $"evaluatedResource contains unexpected type '{extraKey}'.",
                issues: issues);
        }

        foreach (var exType in excludedTypes)
        {
            var bundleHas = bundleCounts.TryGetValue(exType, out var c) && c > 0;
            var evalHas = rawEvalTypes.Contains(exType);

            if (!bundleHas)
            {
                ReportIssue(
                    fileName,
                    resourceType: exType,
                    issueType: ValidationIssueType.ExcludedTypeMissingFromBundle,
                    message: $"Missing required excluded type in bundle: '{exType}' (should exist in bundle but be omitted from evaluatedResource).",
                    issues: issues);
            }

            if (evalHas)
            {
                var issueType = exType.Equals("OperationOutcome", StringComparison.OrdinalIgnoreCase)
                    ? ValidationIssueType.OperationOutcomePresentInEvaluated
                    : ValidationIssueType.ExcludedTypePresentInEvaluated;

                ReportIssue(
                    fileName,
                    resourceType: exType,
                    issueType: issueType,
                    message: $"Excluded type '{exType}' found in evaluatedResource.",
                    issues: issues);
            }
        }

        foreach (var kv in bundleCounts)
        {
            var type = kv.Key;
            if (excludedTypes.Contains(type))
                continue;

            var bundleCount = kv.Value;
            var evalCount = evalCounts.TryGetValue(type, out var n) ? n : 0;

            if (bundleCount != evalCount)
            {
                ReportIssue(
                    fileName,
                    resourceType: type,
                    issueType: ValidationIssueType.BundleVsEvaluatedCountMismatch,
                    message: $"Count mismatch for '{type}': bundle={bundleCount}, evaluatedResource={evalCount}.",
                    issues: issues);
            }
        }

        foreach (var kv in bundleIdsByType)
        {
            var type = kv.Key;
            if (excludedTypes.Contains(type))
                continue;

            var expectedIds = kv.Value;
            var foundIds = evalIdsByType.TryGetValue(type, out var s)
                ? s
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var missing = expectedIds.Where(id => !foundIds.Contains(id))
                                     .OrderBy(x => x)
                                     .ToArray();
            var extra = foundIds.Where(id => !expectedIds.Contains(id))
                                .OrderBy(x => x)
                                .ToArray();

            if (missing.Length > 0)
            {
                ReportIssue(
                    fileName,
                    resourceType: type,
                    issueType: ValidationIssueType.BundleVsEvaluatedIdMismatch,
                    message: $"evaluatedResource missing {type} IDs: [{string.Join(", ", missing)}]",
                    issues: issues);
            }

            if (extra.Length > 0)
            {
                ReportIssue(
                    fileName,
                    resourceType: type,
                    issueType: ValidationIssueType.BundleVsEvaluatedIdMismatch,
                    message: $"evaluatedResource has unexpected {type} IDs: [{string.Join(", ", extra)}]",
                    issues: issues);
            }
        }

        if (crossTypeRefs.Count > 0)
        {
            var details = string.Join("; ",
                crossTypeRefs.Select(x =>
                    $"ref={x.EvaluatedType}/{x.Id} but bundle type={x.ActualType}"));

            ReportIssue(
                fileName,
                resourceType: "",
                issueType: ValidationIssueType.CrossTypeReference,
                message: $"evaluatedResource contains cross-type references: {details}",
                issues: issues);
        }

        if (!issues.Any(i => i.FileName == fileName))
        {
            var bundleSummary = string.Join(", ",
                expectedBundleCounts.OrderBy(k => k.Key)
                                    .Select(kv => $"{kv.Key}={kv.Value}"));

            var evalSummary = string.Join(", ",
                expectedEvalCounts
                    .Where(kv => !kv.Key.Equals("__TOTAL__", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(kv => $"{kv.Key}={kv.Value}"));

            var totalEval = expectedEvalCounts["__TOTAL__"];

            output.WriteLine(
                $"[PASS] {fileName} :: bundle OK [{bundleSummary}] | " +
                $"evaluatedResource OK (total={totalEval}) [{evalSummary}]");
        }
    }
    private static Dictionary<string, int> CountResourceTypes(IEnumerable<JsonElement> lines)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var el in lines)
        {
            if (el.TryGetProperty("resourceType", out var rt) &&
                rt.ValueKind == JsonValueKind.String)
            {
                var key = rt.GetString() ?? string.Empty;
                if (key.Length == 0) continue;

                counts[key] = counts.TryGetValue(key, out var current)
                    ? current + 1
                    : 1;
            }
        }

        return counts;
    }
    private static Dictionary<string, HashSet<string>> BundleIdsByType(IEnumerable<JsonElement> lines)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var el in lines)
        {
            if (!el.TryGetProperty("resourceType", out var rt) ||
                rt.ValueKind != JsonValueKind.String)
                continue;

            if (!el.TryGetProperty("id", out var id) ||
                id.ValueKind != JsonValueKind.String)
                continue;

            var type = rt.GetString() ?? string.Empty;
            var rid = id.GetString() ?? string.Empty;
            if (type.Length == 0 || rid.Length == 0)
                continue;

            if (!map.TryGetValue(type, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                map[type] = set;
            }

            set.Add(rid);
        }

        return map;
    }

    private static (Dictionary<string, int> counts,
                Dictionary<string, HashSet<string>> idsByType,
                List<(string EvaluatedType, string Id, string ActualType)> crossType)
    ParseEvaluatedResource(
        IEnumerable<JsonElement> lines,
        Dictionary<string, HashSet<string>> bundleIdsByType,
        ISet<string> excludedTypes)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var idsByType = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var crossType = new List<(string EvaluatedType, string Id, string ActualType)>();
        var total = 0;

        // Build id -> type index from bundle so we can detect cross-type refs
        var idToType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in bundleIdsByType)
        {
            foreach (var id in kv.Value)
                idToType[id] = kv.Key;
        }

        foreach (var el in lines)
        {
            if (!el.TryGetProperty("resourceType", out var rt) ||
                rt.ValueKind != JsonValueKind.String)
                continue;

            if (!string.Equals(rt.GetString(), "MeasureReport", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!el.TryGetProperty("evaluatedResource", out var eval) ||
                eval.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in eval.EnumerateArray())
            {
                if (!item.TryGetProperty("reference", out var r) ||
                    r.ValueKind != JsonValueKind.String)
                    continue;

                var reference = r.GetString() ?? string.Empty;
                var slash = reference.IndexOf('/');
                if (slash <= 0)
                    continue;

                var type = reference.Substring(0, slash).Trim();
                var id = reference[(slash + 1)..].Trim();
                if (type.Length == 0 || id.Length == 0)
                    continue;

                // ✅ Option A: exclude types do NOT contribute to counts/ids/total
                if (excludedTypes != null && excludedTypes.Contains(type))
                    continue;

                // Only non-excluded types count toward totals
                total++;
                counts[type] = counts.TryGetValue(type, out var n) ? n + 1 : 1;

                if (!idsByType.TryGetValue(type, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    idsByType[type] = set;
                }
                set.Add(id);

                if (idToType.TryGetValue(id, out var actualType) &&
                    !actualType.Equals(type, StringComparison.OrdinalIgnoreCase))
                {
                    crossType.Add((type, id, actualType));
                }
            }
        }

        counts["__TOTAL__"] = total;
        return (counts, idsByType, crossType);
    }


    public void ValidateSingleMeasureAdHocAggregateACHMFile()
    {
        string fileName = "manifest.ndjson";

        var entry = _zipContents.Keys.FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            throw new Exception($"{fileName} is missing from the ZIP archive.");
        var content = _zipContents[entry];

        var measureReportLine = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                        .FirstOrDefault(line => line.Contains("\"resourceType\":\"MeasureReport\"", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(measureReportLine))
            throw new Exception("Verification failed: MeasureReport resource is missing from manifest.ndjson.");

        JObject json = JObject.Parse(measureReportLine);
        int actualCount = (int?)json["group"]?[0]?["population"]?[0]?["count"] ?? -1;
        if (actualCount != 8)
        {
            output.WriteLine($"🔴  [ERROR] MeasureReport count mismatch: Expected 8, Found {actualCount}");
            throw new Exception("Verification failed: MeasureReport 'count' is incorrect.");
        }
        string? measureValue = (string?)json["measure"];
        if (string.IsNullOrWhiteSpace(measureValue) || !measureValue.Contains("|"))
        {
            output.WriteLine($"🔴  [ERROR] MeasureReport 'measure' value is missing or malformed: '{measureValue}'");
            throw new Exception("Verification failed: MeasureReport 'measure' field is missing or malformed.");
        }
        string version = measureValue.Split('|').Last();
        if (version != SingleMeasureAdHocAchDqmVersion)
        {
            output.WriteLine($"🔴  [ERROR] MeasureReport version mismatch: Expected '{SingleMeasureAdHocAchDqmVersion}', Found '{version}'");
            throw new Exception("Verification failed: MeasureReport 'measure' version is incorrect.");
        }
        output.WriteLine($"[PASS] aggregate-ACHM.json: 'count' == 8 and 'measure' version == '{SingleMeasureAdHocAchDqmVersion}'.");
    }
    public async Task WaitForSingleMeasureZipContentsAsync(
    int timeoutInSeconds = 600,
    int stableCycles = 5,
    int pollingIntervalMs = 3000)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutInSeconds);
        int attempt = 0;
        int stableCount = 0;
        HashSet<string>? previousNames = null;
        string? lastError = null;

        var requiredFiles = TestConfig.SingleMeasureExpectedFiles;

        output.WriteLine("[INFO] Waiting for ZIP contents to stabilize and contain all expected files…");

        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            try
            {
                await DownloadAndExtractSingleMeasureZipAsync();

                var currentNames = _zipContents.Keys
                    .Where(n => n.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var haveAllRequired = requiredFiles.All(req =>
                    currentNames.Any(n => n.EndsWith(req, StringComparison.OrdinalIgnoreCase)));

                if (!haveAllRequired)
                {
                    stableCount = 0;
                }
                else
                {
                    if (previousNames != null && currentNames.SetEquals(previousNames))
                    {
                        stableCount++;
                    }
                    else
                    {
                        stableCount = 1;
                    }
                }

                previousNames = currentNames;
                lastError = null;

                if (stableCount >= stableCycles)
                {
                    output.WriteLine(
                        $"[INFO] ZIP contents stable after {attempt} poll(s). File count: {currentNames.Count}");
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = $"Poll {attempt} failed: {ex.Message}";
                stableCount = 0;
            }

            await Task.Delay(pollingIntervalMs);
        }

        if (lastError != null)
            output.WriteLine($"[WARN] Last poll failure: {lastError}");

        var finalNames = _zipContents.Keys
            .Where(n => n.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = requiredFiles
            .Where(req => !finalNames.Any(n => n.EndsWith(req, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var foundList = finalNames.Count == 0
            ? "<no ndjson entries>"
            : string.Join(", ", finalNames);

        var missingList = missing.Length == 0
            ? "<none>"
            : string.Join(", ", missing);

        throw new TimeoutException(
            $"🔴 ZIP did not reach a stable state with all expected files within {timeoutInSeconds}s " +
            $"after {attempt} poll(s). Missing: {missingList}. Found: {foundList}");
    }
    private static void ValidateResourceTypeCounts(
        Dictionary<string, int> actualCounts,
        Dictionary<string, int> expectedCounts)
    {
        foreach (var kvp in expectedCounts)
        {
            var key = kvp.Key;
            var expected = kvp.Value;
            var actual = actualCounts.TryGetValue(key, out var n) ? n : 0;

            if (actual != expected)
            {
                var actualSummary = string.Join(", ", actualCounts.Select(x => $"{x.Key}={x.Value}"));
                throw new Exception(
                    $"manifest.ndjson resourceType count mismatch for '{key}': " +
                    $"expected {expected}, got {actual}. All counts: {actualSummary}");
            }
        }
    }
    private static void ValidateOperationOutcomeIssuesForPatients(
        List<JsonElement> manifestLines,
        IEnumerable<string> expectedPatientIds)
    {
        JsonElement operationOutcome = default;
        var found = false;

        foreach (var el in manifestLines)
        {
            if (el.TryGetProperty("resourceType", out var rt) &&
                string.Equals(rt.GetString(), "OperationOutcome", StringComparison.OrdinalIgnoreCase))
            {
                operationOutcome = el;
                found = true;
                break;
            }
        }

        if (!found)
            throw new Exception("OperationOutcome resource not found in manifest.ndjson.");

        if (!operationOutcome.TryGetProperty("issue", out var issuesElement) ||
            issuesElement.ValueKind != JsonValueKind.Array)
        {
            throw new Exception("OperationOutcome.issue array missing or invalid in manifest.ndjson.");
        }

        var actualPatientIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var issue in issuesElement.EnumerateArray())
        {
            if (issue.TryGetProperty("severity", out var severity) &&
                severity.ValueKind == JsonValueKind.String &&
                !string.Equals(severity.GetString(), "fatal", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"OperationOutcome.issue.severity expected 'fatal' but found '{severity.GetString()}'.");
            }

            if (issue.TryGetProperty("code", out var code) &&
                code.ValueKind == JsonValueKind.String &&
                !string.Equals(code.GetString(), "invalid", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"OperationOutcome.issue.code expected 'invalid' but found '{code.GetString()}'.");
            }

            if (!issue.TryGetProperty("diagnostics", out var diag) ||
                diag.ValueKind != JsonValueKind.String)
            {
                throw new Exception("OperationOutcome.issue.diagnostics missing or not a string.");
            }

            var diagText = diag.GetString() ?? string.Empty;

            const string prefix = "Validation failed for patient ";
            if (!diagText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Unexpected OperationOutcome.issue.diagnostics format: '{diagText}'.");
            }

            var patientId = diagText.Substring(prefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(patientId))
            {
                throw new Exception($"Could not extract patient ID from diagnostics: '{diagText}'.");
            }

            actualPatientIds.Add(patientId);
        }

        var expectedSet = new HashSet<string>(expectedPatientIds, StringComparer.OrdinalIgnoreCase);

        if (!expectedSet.SetEquals(actualPatientIds))
        {
            string expectedList = string.Join(", ", expectedSet);
            string actualList = string.Join(", ", actualPatientIds);

            throw new Exception(
                $"OperationOutcome patient IDs mismatch. " +
                $"Expected [{expectedList}], found [{actualList}].");
        }
    }
    private static void ValidateListSnapshotBlockForPatients(
        List<JsonElement> manifestLines,
        string status,
        string mode,
        IEnumerable<string> expectedPatientRefs)
    {
        JsonElement listResource = default;
        var found = false;

        foreach (var el in manifestLines)
        {
            if (el.TryGetProperty("resourceType", out var rt) &&
                string.Equals(rt.GetString(), "List", StringComparison.OrdinalIgnoreCase))
            {
                listResource = el;
                found = true;
                break;
            }
        }

        if (!found)
            throw new Exception("List resource not found in manifest.ndjson.");

        if (!listResource.TryGetProperty("status", out var statusElement) ||
            statusElement.ValueKind != JsonValueKind.String ||
            !string.Equals(statusElement.GetString(), status, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"List.status expected '{status}' but found '{statusElement.GetString()}'.");
        }

        if (!listResource.TryGetProperty("mode", out var modeElement) ||
            modeElement.ValueKind != JsonValueKind.String ||
            !string.Equals(modeElement.GetString(), mode, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"List.mode expected '{mode}' but found '{modeElement.GetString()}'.");
        }

        if (!listResource.TryGetProperty("entry", out var entryElement) ||
            entryElement.ValueKind != JsonValueKind.Array)
        {
            throw new Exception("List.entry array missing or invalid in manifest.ndjson.");
        }

        var actualRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entryElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("item", out var itemElement) ||
                itemElement.ValueKind != JsonValueKind.Object ||
                !itemElement.TryGetProperty("reference", out var refElement) ||
                refElement.ValueKind != JsonValueKind.String)
            {
                throw new Exception("List.entry.item.reference missing or invalid in manifest.ndjson.");
            }

            var reference = refElement.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new Exception("List.entry.item.reference is empty or whitespace.");
            }

            actualRefs.Add(reference);
        }

        var expectedSet = new HashSet<string>(expectedPatientRefs, StringComparer.OrdinalIgnoreCase);

        if (!expectedSet.SetEquals(actualRefs))
        {
            string expectedList = string.Join(", ", expectedSet);
            string actualList = string.Join(", ", actualRefs);

            throw new Exception(
                $"List.entry.item.reference mismatch. " +
                $"Expected [{expectedList}], found [{actualList}].");
        }
    }

    private static readonly Dictionary<ValidationIssueType, ValidationSeverity> SeverityConfig
    = new Dictionary<ValidationIssueType, ValidationSeverity>
    {

        [ValidationIssueType.MissingResourceTypeInBundle] = ValidationSeverity.Error,
        [ValidationIssueType.BundleCountMismatch] = ValidationSeverity.Error,
        [ValidationIssueType.EvaluatedCountMismatch] = ValidationSeverity.Error,
        [ValidationIssueType.UnexpectedEvaluatedType] = ValidationSeverity.Error,
        [ValidationIssueType.ExcludedTypeMissingFromBundle] = ValidationSeverity.Error,
        [ValidationIssueType.ExcludedTypePresentInEvaluated] = ValidationSeverity.Error,
        [ValidationIssueType.BundleVsEvaluatedCountMismatch] = ValidationSeverity.Error,
        [ValidationIssueType.BundleVsEvaluatedIdMismatch] = ValidationSeverity.Error,
        [ValidationIssueType.CrossTypeReference] = ValidationSeverity.Error,
        [ValidationIssueType.OperationOutcomePresentInEvaluated] = ValidationSeverity.Info,
        [ValidationIssueType.ObservedResourceTypeCount] = ValidationSeverity.Info,
    };

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value ?? string.Empty;

        // leave space for "..."
        return value.Substring(0, maxLength - 3) + "...";
    }
    private static HashSet<string> GetEvaluatedTypesRaw(IEnumerable<JsonElement> lines)
    {
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var el in lines)
        {
            if (!el.TryGetProperty("resourceType", out var rt) || rt.ValueKind != JsonValueKind.String)
                continue;

            if (!string.Equals(rt.GetString(), "MeasureReport", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!el.TryGetProperty("evaluatedResource", out var eval) || eval.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in eval.EnumerateArray())
            {
                if (!item.TryGetProperty("reference", out var r) || r.ValueKind != JsonValueKind.String)
                    continue;

                var reference = r.GetString() ?? string.Empty;
                var slash = reference.IndexOf('/');
                if (slash <= 0)
                    continue;

                var type = reference.Substring(0, slash).Trim();
                if (!string.IsNullOrWhiteSpace(type))
                    types.Add(type);
            }
        }

        return types;
    }

    private void ReportIssue(
        string fileName,
        string resourceType,
        ValidationIssueType issueType,
        string message,
        List<ValidationIssue> issues)
    {
        var severity = SeverityConfig[issueType];
        issues.Add(new ValidationIssue(
            fileName: fileName,
            issueType: issueType,
            severity: severity,
            resourceType: resourceType,
            message: message));
    }

    public void ValidateAllPatientsWithConfigurableSeverity()
    {
        var issues = new List<ValidationIssue>();

        ValidatePatientFile_1(issues);
        ValidatePatientFile_2(issues);
        ValidatePatientFile_3(issues);
        ValidatePatientFile_4(issues);
        ValidatePatientFile_5(issues);
        ValidatePatientFile_6(issues);

        if (issues.Count == 0)
        {
            output.WriteLine("✅ All patients passed validation with no issues.");
            return;
        }
        output.WriteLine(string.Empty);
        output.WriteLine("==========================================");
        output.WriteLine("        PATIENT VALIDATION SUMMARY");
        output.WriteLine("==========================================");
        output.WriteLine(string.Empty);

        var groups = issues
            .GroupBy(i => i.FileName)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            output.WriteLine("------------------------------------------");
            output.WriteLine($" PATIENT FILE: {group.Key}");
            output.WriteLine("------------------------------------------");
            output.WriteLine(string.Empty);

            int issueW = Clamp(
                Math.Max("Issue Type".Length, group.Max(i => i.IssueType.ToString().Length)),
                min: 18,
                max: 32);

            int sevW = Clamp(
                Math.Max("Severity".Length, group.Max(i => i.Severity.ToString().Length)),
                min: 7,
                max: 10);

            int resW = Clamp(
                Math.Max("Resource".Length, group.Max(i => (i.ResourceType ?? string.Empty).Length)),
                min: 10,
                max: 22);

            string header =
                $"| {Cell("Issue Type", issueW)} | {Cell("Severity", sevW)} | {Cell("Resource", resW)} | Description";

            output.WriteLine(header);
            output.WriteLine(new string('-', header.Length));
            ValidationIssueType? previousType = null;

            foreach (var issue in group
                .OrderBy(i => i.IssueType == ValidationIssueType.EvaluatedCountMismatch ? 1 : 0)
                .ThenBy(i =>
                    i.IssueType == ValidationIssueType.EvaluatedCountMismatch &&
                    string.Equals(i.ResourceType, "__TOTAL__", StringComparison.OrdinalIgnoreCase)
                        ? 1
                        : 0)
                .ThenBy(i => i.ResourceType, StringComparer.OrdinalIgnoreCase))
            {
                if (previousType != null && previousType != issue.IssueType)
                {
                    output.WriteLine(string.Empty);
                }

                string description = issue.Message ?? string.Empty;

                if (issue.IssueType == ValidationIssueType.EvaluatedCountMismatch &&
                    string.Equals(issue.ResourceType, "__TOTAL__", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = description.IndexOf("Breakdown:", StringComparison.OrdinalIgnoreCase);
                    if (idx > 0)
                        description = description[..idx].TrimEnd();
                }

                string row =
                    $"| {Cell(issue.IssueType.ToString(), issueW)} | {Cell(issue.Severity.ToString(), sevW)} | {Cell(issue.ResourceType, resW)} | {description}";

                output.WriteLine(row);
                previousType = issue.IssueType;
            }

            output.WriteLine(string.Empty);
        }

        int totalWarnings = issues.Count(i => i.Severity == ValidationSeverity.Warning);
        int totalErrors = issues.Count(i => i.Severity == ValidationSeverity.Error);

        output.WriteLine("==========================================");
        output.WriteLine($"TOTAL WARNINGS (all patients): {totalWarnings}");
        output.WriteLine($"TOTAL ERRORS   (all patients): {totalErrors}");
        output.WriteLine("==========================================");
        output.WriteLine(string.Empty);
        output.WriteLine(string.Empty);

        var totalBreakdowns = issues
            .Where(i =>
                i.IssueType == ValidationIssueType.EvaluatedCountMismatch &&
                string.Equals(i.ResourceType, "__TOTAL__", StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.FileName)
            .ToList();

        if (totalBreakdowns.Count > 0)
        {
            output.WriteLine("Detailed evaluatedResource '__TOTAL__' breakdowns:");
            foreach (var issue in totalBreakdowns)
            {
                output.WriteLine($"- {issue.FileName}: {issue.Message}");
            }
            output.WriteLine(string.Empty);
        }
        bool hasError = issues.Any(i => i.Severity == ValidationSeverity.Error);

        if (hasError)
        {
            throw new Exception(
                $"Found {issues.Count(i => i.Severity == ValidationSeverity.Error)} error-level validation issue(s). " +
                "See summary table above.");
        }

        output.WriteLine("🟠 Validation completed with warnings but no errors.");
    }

    private static string Cell(string? value, int width)
    {
        value ??= string.Empty;
        if (value.Length > width)
            return value.Substring(0, Math.Max(0, width - 1)) + "…"; 

        return value.PadRight(width);
    }

    private static int Clamp(int value, int min, int max)
        => Math.Max(min, Math.Min(max, value));
}

