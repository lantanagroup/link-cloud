using LantanaGroup.Link.Tests.E2ETests;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;

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
            "patient-jbbPDJeGWyEyudcf6EBKTgmeCLxB7jTgu5Ugm27JAO494.ndjson",
            "patient-DJxsHpmWuBezhV9hJNgEHT4szaKW3uP5vUNzXUCkltpXj.ndjson",
            "patient-9i6Xi6uG2WjuGxHTmpbin4ct2ZwevRwTWhIkJkRjVFZ4C.ndjson",
            "patient-5ieWogP3EGV24Kus8QsGh6rpmUaJBP5Hl0nCSJJXmh6TI.ndjson"
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



    public void SingleMeasureAdHocValidateManifestContent()
    {
        const string manifestName = "manifest.ndjson";

        // 1️⃣ Locate manifest.ndjson
        var manifestEntry = _zipContents
            .FirstOrDefault(kvp => kvp.Key.EndsWith("manifest.ndjson", StringComparison.OrdinalIgnoreCase));

        if (manifestEntry.Key == null)
            throw new Exception("manifest.ndjson not found in ZIP archive.");

        var manifestText = manifestEntry.Value;

        if (string.IsNullOrWhiteSpace(manifestText))
            throw new Exception("manifest.ndjson is empty.");

        // 2️⃣ Parse NDJSON content into JsonElements
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var manifestLines = manifestText
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line, jsonOptions))
            .ToList();

        if (manifestLines.Count == 0)
            throw new Exception("manifest.ndjson contained only whitespace.");

        // 3️⃣ Build resourceType counts
        var counts = BuildResourceTypeCounts(manifestLines);

        // Optional but recommended: ensure we have the 5 resources we expect
        var expectedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Organization"] = 1,
            ["Device"] = 1,
            ["List"] = 1,
            ["MeasureReport"] = 1,
            ["OperationOutcome"] = 1
        };

        ValidateResourceTypeCounts(counts, expectedCounts);

        // 4️⃣ Validate OperationOutcome issues (one issue per patient)
        ValidateOperationOutcomeIssuesForPatients(
            manifestLines,
            SingleMeasureExpectedPatientIDs);

        // 5️⃣ Validate List snapshot block
        var expectedPatientRefs = SingleMeasureExpectedPatientIDs
            .Select(id => $"Patient/{id}")
            .ToArray();

        ValidateListSnapshotBlockForPatients(
            manifestLines,
            status: "current",
            mode: "snapshot",
            expectedPatientRefs: expectedPatientRefs);

        // 6️⃣ If we got this far, everything passed
        var countsString = string.Join(", ", counts.Select(kv => $"{kv.Key}={kv.Value}"));
        output.WriteLine($"[PASS] manifest.ndjson validation passed. ResourceType counts: {countsString}");
    }










    public void ValidateSpecificPatientFileContents(int timeoutSeconds = 10, int pollIntervalMs = 1000)
    {
        string fileName = "patient-x25sJU80vVa51mxJ6vSDcjbNC3BcdCQujJbXQwqdppFOO.ndjson";

        var entry = _zipContents.Keys.FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            throw new Exception($"{fileName} is missing from the ZIP archive.");

        var content = _zipContents[entry];
        JObject json = null;

        var expectedResourceCounts = new Dictionary<string, int>
            {
                { "Encounter", 2 },
                { "Observation", 23 },
                { "Device", 1 },
                { "MedicationRequest", 4 },
                { "Procedure", 3 },
                { "Condition", 4 },
                { "Patient", 1 },
                { "Coverage", 2 },
                { "DiagnosticReport", 2 },
                { "MeasureReport", 1 },
                { "ServiceRequest", 116 },
                { "Location", 2 },
                {"Medication", 4 }
            };
        Dictionary<string, int> actualCounts = null;
        DateTime startTime = DateTime.Now;
        while ((DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
        {
            //the content is ndjson, so we need to split it into lines and parse each line as JSON
            foreach (var line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var lineJson = JObject.Parse(line);
                var resourceType = (string)lineJson["resourceType"] ?? "null";
                
                if (actualCounts == null)
                    actualCounts = new Dictionary<string, int>();
                if (actualCounts.ContainsKey(resourceType))
                    actualCounts[resourceType]++;
                else
                    actualCounts[resourceType] = 1;

                var entryCounts = lineJson["entry"]?
                    .GroupBy(e => (string)e["resource"]?["resourceType"])
                    .ToDictionary(g => g.Key ?? "null", g => g.Count()) ?? new Dictionary<string, int>();

                foreach (var kvp in entryCounts)
                {
                    if (actualCounts.ContainsKey(kvp.Key))
                        actualCounts[kvp.Key] += kvp.Value;
                    else
                        actualCounts[kvp.Key] = kvp.Value;
                }
            }

            if (expectedResourceCounts.All(kvp =>
                actualCounts.TryGetValue(kvp.Key, out int actual) && actual >= kvp.Value))
            {
                break;
            }
            Thread.Sleep(pollIntervalMs);
        }

        if (actualCounts == null)
            throw new Exception("Validation failed: Could not parse resourceType counts from JSON content.");
        var mismatches = new List<string>();
        var unexpected = new List<string>();

        foreach (var expected in expectedResourceCounts)
        {
            actualCounts.TryGetValue(expected.Key, out int actualCount);
            if (actualCount != expected.Value)
            {
                mismatches.Add($"🔴 [ERROR] ResourceType '{expected.Key}': Expected {expected.Value}, Found {actualCount}");
            }
        }

        foreach (var actual in actualCounts.Keys)
        {
            if (!expectedResourceCounts.ContainsKey(actual))
            {
                unexpected.Add($"[WARNING] Unexpected resourceType found: '{actual}' (Count: {actualCounts[actual]})");
            }
        }
        foreach (var line in mismatches.Concat(unexpected))
            output.WriteLine(line);
        if (mismatches.Any())
            throw new Exception("Validation failed: One or more expected resourceType counts are incorrect.");
        output.WriteLine("[PASS] All expected resourceType counts match, and no unexpected types found.");
    }
    public void ValidateSingleMeasureAdHocAggregateACHMFile()
    {
        string fileName = "manifest.ndjson";

        var entry = _zipContents.Keys.FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            throw new Exception($"{fileName} is missing from the ZIP archive.");
        var content = _zipContents[entry];

        //loop through each line and find the MeasureReport resource
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
        int stableCycles = 60,
        List<string>? requiredFiles = null,
        int pollingIntervalMs = 3000)
        {
        DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutInSeconds);
        int attempt = 0;
        int stableCount = 0;
        HashSet<string>? previousNames = null;
        string? lastError = null;

        output.WriteLine("[INFO] Waiting for ZIP contents to stabilize…");

        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            try
            {
                await DownloadAndExtractSingleMeasureZipAsync();
                var currentNames = _zipContents.Keys
                                               .Where(n => n.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase))
                                               .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (requiredFiles != null &&
                    !requiredFiles.All(req => currentNames.Any(n => n.EndsWith(req, StringComparison.OrdinalIgnoreCase))))
                {
                    stableCount = 0;
                }
                else
                {
                    if (previousNames != null && currentNames.SetEquals(previousNames))
                        stableCount++;
                    else
                        stableCount = 0;
                }

                previousNames = currentNames;
                lastError = null; 

                if (stableCount >= stableCycles)
                {
                    if (lastError != null)
                        output.WriteLine($"[WARN] Last poll failure: {lastError}");

                    output.WriteLine($"[INFO] ZIP contents stable after {attempt} poll(s). File count: {currentNames.Count}");
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

        throw new TimeoutException(
            $"🔴 ZIP did not reach a stable state within {timeoutInSeconds}s after {attempt} poll(s).");
    }


    private static Dictionary<string, int> BuildResourceTypeCounts(IEnumerable<JsonElement> manifestLines)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var el in manifestLines)
        {
            if (el.TryGetProperty("resourceType", out var rt) &&
                rt.ValueKind == JsonValueKind.String)
            {
                var key = rt.GetString() ?? string.Empty;
                if (key.Length == 0) continue;

                counts[key] = counts.TryGetValue(key, out var current) ? current + 1 : 1;
            }
        }

        return counts;
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
        // Find the OperationOutcome resource
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
            // Optional extra checks (severity, code)
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

            // Your manifest uses: "Validation failed for patient {id}"
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
        // Find the List resource
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






}

