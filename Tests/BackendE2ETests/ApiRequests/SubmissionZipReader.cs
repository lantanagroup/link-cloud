using LantanaGroup.Link.Tests.E2ETests;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.BackendE2ETests.ApiRequests;

public class SubmissionZipReader(ITestOutputHelper output)
{
    protected static readonly string api_LinkAdminBffURL = TestConfig.AdminBffBase;
    protected static readonly string fhirServerBaseUrl = TestConfig.InternalFhirServerBase;
    protected static readonly string SingleMeasureAdHocFacility = TestConfig.SingleMeasureAdHocFacility;
    protected static readonly string SingleMeasureAdHocAchDqmVersion = TestConfig.SingleMeasureAdHocAchDqmVersion;
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
        var expectedFiles = new List<string>
        {
            "manifest.ndjson",
            "patient-x25sJU80vVa51mxJ6vSDcjbNC3BcdCQujJbXQwqdppFOO.ndjson",
            "patient-MVLkMLWErl3gQGRCuA2mygtVuix7PMBFBh9WVayaCL7xM.ndjson",
            "patient-CYUcGIlSrpJxCBMeEml30YSmE0Ea7loNBPVZfhCUkv7A3.ndjson",
            "patient-VsZkAG8h9vkGcL528ZcJxVXynyj8X39GaDfjHbA9AnvyA.ndjson",
            "patient-jjMZxCVWUbZgLkPf2LTzvZIBOW76YLJdIGCw8JFaTPiZg.ndjson",
            "patient-6tZ8Wt8maJdDFLvEsDcKmAaCAcSOxjr0mB8RjEi5Szw7H.ndjson"
        };

        var missingFiles = expectedFiles
            .Where(expected => !_zipContents.Keys.Any(actual => actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase)))
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
}

