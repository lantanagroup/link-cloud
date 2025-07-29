using System.IO.Compression;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;
using LantanaGroup.Link.Tests.E2ETests;


namespace LantanaGroup.Link.Tests.BackendE2ETests.ApiRequests
{
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

            if (save && !string.IsNullOrEmpty(TestConfig.SmokeTestDownloadPath))
            {
                if (!Directory.Exists(TestConfig.SmokeTestDownloadPath))
                    Directory.CreateDirectory(TestConfig.SmokeTestDownloadPath);

                var downloadPath = Path.Combine(TestConfig.SmokeTestDownloadPath, "adhoc-reporting-smoke-test-submission.zip");
                await File.WriteAllBytesAsync(downloadPath, zipBytes);
                output.WriteLine($"Report downloaded to {downloadPath}");
            }

            using var zipStream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
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
                "patient-list.json",
                "sending-organization.json",
                "sending-device.json",
                "aggregate-ACHM.json",
                "other-resources.json",
                "patient-x25sJU80vVa51mxJ6vSDcjbNC3BcdCQujJbXQwqdppFOO.json",
                "patient-MVLkMLWErl3gQGRCuA2mygtVuix7PMBFBh9WVayaCL7xM.json",
                "patient-CYUcGIlSrpJxCBMeEml30YSmE0Ea7loNBPVZfhCUkv7A3.json",
                "patient-VsZkAG8h9vkGcL528ZcJxVXynyj8X39GaDfjHbA9AnvyA.json",
                "patient-jjMZxCVWUbZgLkPf2LTzvZIBOW76YLJdIGCw8JFaTPiZg.json",
                "patient-6tZ8Wt8maJdDFLvEsDcKmAaCAcSOxjr0mB8RjEi5Szw7H.json"
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
            var disallowedFiles = new List<string>
            {
                "patient-jbbPDJeGWyEyudcf6EBKTgmeCLxB7jTgu5Ugm27JAO494.json",
                "patient-DJxsHpmWuBezhV9hJNgEHT4szaKW3uP5vUNzXUCkltpXj.json",
                "patient-9i6Xi6uG2WjuGxHTmpbin4ct2ZwevRwTWhIkJkRjVFZ4C.json",
                "patient-5ieWogP3EGV24Kus8QsGh6rpmUaJBP5Hl0nCSJJXmh6TI.json"
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
            string fileName = "patient-x25sJU80vVa51mxJ6vSDcjbNC3BcdCQujJbXQwqdppFOO.json";

            var entry = _zipContents.Keys.FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                throw new Exception($"{fileName} is missing from the ZIP archive.");

            var content = _zipContents[entry];
            JObject json = null;

            var expectedResourceCounts = new Dictionary<string, int>
                {
                    { "Bundle", 1 },
                    { "Encounter", 2 },
                    { "Observation", 23 },
                    { "Device", 1 },
                    { "MedicationRequest", 4 },
                    { "Procedure", 3 },
                    { "Condition", 4 },
                    { "Patient", 1 },
                    { "Coverage", 2 },
                    { "DiagnosticReport", 2 },
                    { "MeasureReport", 1 }
                };
            Dictionary<string, int> actualCounts = null;
            DateTime startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
            {
                json = JObject.Parse(content);

                actualCounts = new Dictionary<string, int>
                {
                    { (string)json["resourceType"] ?? "null", 1 }
                };
                var entryCounts = json["entry"]?
                    .GroupBy(e => (string)e["resource"]?["resourceType"])
                    .ToDictionary(g => g.Key ?? "null", g => g.Count()) ?? new Dictionary<string, int>();

                foreach (var kvp in entryCounts)
                {
                    if (actualCounts.ContainsKey(kvp.Key))
                        actualCounts[kvp.Key] += kvp.Value;
                    else
                        actualCounts[kvp.Key] = kvp.Value;
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
            string fileName = "aggregate-ACHM.json";

            var entry = _zipContents.Keys.FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                throw new Exception($"{fileName} is missing from the ZIP archive.");
            var content = _zipContents[entry];
            JObject json = JObject.Parse(content);
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
            int timeoutInSeconds = 300,
            int stableCycles = 5,
            List<string>? requiredFiles = null,
            int pollingIntervalMs = 1000)
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
                                                   .Where(n => n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
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
}

