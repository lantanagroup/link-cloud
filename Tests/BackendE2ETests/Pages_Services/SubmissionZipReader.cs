using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;
using API_Integration.Pages;

namespace LantanaGroup.Link.Tests.BackendE2ETests.Pages_Services
{
   
    public class SubmissionZipReader : ApiBasePage
    {
        public TestContext TestContext { get; set; }
        private readonly HttpClient _client = new HttpClient();
        private readonly Dictionary<string, string> _zipContents = new();
        string AdHocReportGuid => TestContextStore.AdHocReportTrackingIdGuid;

        public async Task DownloadAndExtractSingleMeasureZipAsync()
        {
            if (string.IsNullOrEmpty(singleMeasureAdHocFacility))
                throw new InvalidOperationException("Facility ID must be set using UseSingleMeasureFacility() or UseMultiMeasureFacility().");

            var url = $"{api_LinkAdminBffURL}/api/Submission/{singleMeasureAdHocFacility}/{AdHocReportGuid}";
            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            byte[] zipBytes = await response.Content.ReadAsByteArrayAsync();
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
                "aggregate-ACH.json",
                "other-resources.json",
                "patient-Patient-multi10.json",
                "patient-Patient-HYPOAPR1.json",
                "patient-Patient-HYPOAPR2.json",
                "patient-Patient-May1.json",
                "patient-Patient-multi6.json"
            };

            var missingFiles = expectedFiles
                .Where(expected => !_zipContents.Keys.Any(actual => actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (missingFiles.Any())
            {
                foreach (var file in missingFiles)
                    TestContext?.WriteLine($"[ERROR] {file} is missing.");

                string fileList = string.Join(", ", missingFiles);
                throw new Exception($"Validation failed: {missingFiles.Count} file(s) missing: {fileList}");
            }

            TestContext?.WriteLine("[PASS] All expected files appear in the ZIP archive.");
        }
        public void SingleMeasureAdHocValidateFilesDoNotAppear()
        {
            var disallowedFiles = new List<string>
            {
                "patient-Patient-multi8.json",
                "patient-Patient-multi9.json",
                "patient-Patient-Jume1.json",
                "patient-Patient-multi13.json",
                "patient-Patient-multi14.json"
            };

            var foundDisallowedFiles = disallowedFiles
                .Where(disallowed => _zipContents.Keys.Any(actual => actual.EndsWith(disallowed, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (foundDisallowedFiles.Any())
            {
                foreach (var file in foundDisallowedFiles)
                    TestContext?.WriteLine($"[ERROR] {file} was found but should NOT be present.");

                throw new Exception($"Validation failed: {foundDisallowedFiles.Count} disallowed file(s) were found.");
            }

            TestContext?.WriteLine("[PASS] No disallowed files were found in the ZIP archive.");
        }
        public void ValidatePatientHypoAPR2FileContents()
        {
            string fileName = "patient-Patient-HYPOAPR2.json";

            var entry = _zipContents.Keys.FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                throw new Exception($"{fileName} is missing from the ZIP archive.");

            var content = _zipContents[entry];
            JObject json = JObject.Parse(content);
            List<string> expectedEvaluatedReferences = new()
            {
                "Coverage/autopmCoverageId52", "Patient/Patient-HYPOAPR2", "Encounter/Encounter1-HYPOAPR2",
                "Coverage/autopmCoverageId28", "Coverage/autopmCoverageId658", "Medication/Medication-HYPOAPR2",
                "Location/Location-multi2", "Coverage/autopmCoverageId882", "DiagnosticReport/DR-Lab-HYPOAPR2",
                "Coverage/autopmCoverageId223", "Coverage/Coverage-HYPOAPR2", "Coverage/autopmCoverageId342",
                "Observation/Obs-lab-HYPOAPR2", "Coverage/autopmCoverageId865", "MedicationRequest/MedicationRequest1-HYPOAPR2",
                "Encounter/Encounter-HYPOAPR2", "Procedure/Procedure-HYPOAPR2", "Specimen/Specimen-HYPOAPR2",
                "MedicationRequest/MR-HYPOAPR2"
            };

            var evaluatedRefs = json["entry"]
                ?.FirstOrDefault(e => (string)e["resource"]?["resourceType"] == "MeasureReport")?["resource"]?["evaluatedResource"]
                ?.Select(r => (string)r["reference"])
                .Where(r => r != null)
                .ToList() ?? new List<string>();

            var missingEvalRefs = expectedEvaluatedReferences.Except(evaluatedRefs).ToList();
            if (missingEvalRefs.Any())
            {
                TestContext?.WriteLine("[ERROR] Missing evaluatedResource references:");
                foreach (var missing in missingEvalRefs)
                    TestContext?.WriteLine($" - {missing}");

                throw new Exception("Validation failed: evaluatedResource references are incomplete.");
            }
            var expectedEntries = new List<(string fullUrl, string resourceType, string id, string subject)>
            {
                ("https://www.cdc.gov/nhsn/nhsn-measures/Coverage/autopmCoverageId52", "Coverage", "autopmCoverageId52", null),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Patient/Patient-HYPOAPR2", "Patient", "Patient-HYPOAPR2", null),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Encounter/Encounter1-HYPOAPR2", "Encounter", "Encounter1-HYPOAPR2", "Patient/Patient-HYPOAPR2"),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Coverage/autopmCoverageId28", "Coverage", "autopmCoverageId28", null),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Coverage/autopmCoverageId658", "Coverage", "autopmCoverageId658", null),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Coverage/autopmCoverageId882", "Coverage", "autopmCoverageId882", null),
                ("https://www.cdc.gov/nhsn/nhsn-measures/DiagnosticReport/DR-Lab-HYPOAPR2", "DiagnosticReport", "DR-Lab-HYPOAPR2", "Patient/Patient-HYPOAPR2"),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Coverage/autopmCoverageId223", "Coverage", "autopmCoverageId223", null),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Coverage/Coverage-HYPOAPR2", "Coverage", "Coverage-HYPOAPR2", null),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Coverage/autopmCoverageId342", "Coverage", "autopmCoverageId342", null),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Observation/Obs-lab-HYPOAPR2", "Observation", "Obs-lab-HYPOAPR2", "Patient/Patient-HYPOAPR2"),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Coverage/autopmCoverageId865", "Coverage", "autopmCoverageId865", null),
                ("https://www.cdc.gov/nhsn/nhsn-measures/MedicationRequest/MedicationRequest1-HYPOAPR2", "MedicationRequest", "MedicationRequest1-HYPOAPR2", "Patient/Patient-HYPOAPR2"),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Encounter/Encounter-HYPOAPR2", "Encounter", "Encounter-HYPOAPR2", "Patient/Patient-HYPOAPR2"),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Procedure/Procedure-HYPOAPR2", "Procedure", "Procedure-HYPOAPR2", "Patient/Patient-HYPOAPR2"),
                ("https://www.cdc.gov/nhsn/nhsn-measures/Specimen/Specimen-HYPOAPR2", "Specimen", "Specimen-HYPOAPR2", "Patient/Patient-HYPOAPR2"),
                ("https://www.cdc.gov/nhsn/nhsn-measures/MedicationRequest/MR-HYPOAPR2", "MedicationRequest", "MR-HYPOAPR2", "Patient/Patient-HYPOAPR2"),
                ("https://www.cdc.gov/nhsn/nhsn-measures/MeasureReport/", "MeasureReport", null, "Patient/Patient-HYPOAPR2")
            };

            var jsonEntries = json["entry"]?.ToList() ?? new List<JToken>();

            foreach (var (fullUrlPrefix, resourceType, id, subject) in expectedEntries)
            {
                var match = jsonEntries.FirstOrDefault(e =>
                {
                    string fullUrlValue = (string)e["fullUrl"];
                    return fullUrlValue != null && fullUrlValue.StartsWith(fullUrlPrefix);
                });

                if (match == null)
                {
                    TestContext?.WriteLine($"[ERROR] Missing fullUrl starting with: {fullUrlPrefix}");
                    throw new Exception($"Validation failed: Missing fullUrl starting with {fullUrlPrefix}");
                }

                var resource = match["resource"];

                if ((string)resource["resourceType"] != resourceType)
                {
                    TestContext?.WriteLine($"[ERROR] Incorrect resourceType for fullUrl starting with {fullUrlPrefix}: Expected '{resourceType}', Found '{(string)resource["resourceType"]}'");
                    throw new Exception($"Validation failed: resourceType mismatch at fullUrl starting with {fullUrlPrefix}");
                }

                if (id != null && (string)resource["id"] != id)
                {
                    TestContext?.WriteLine($"[ERROR] Incorrect id for fullUrl starting with {fullUrlPrefix}: Expected '{id}', Found '{(string)resource["id"]}'");
                    throw new Exception($"Validation failed: resource id mismatch at fullUrl starting with {fullUrlPrefix}");
                }

                if (subject != null)
                {
                    string actualSubject = (string)resource["subject"]?["reference"] ?? (string)resource["subject"];
                    if (actualSubject != subject)
                    {
                        TestContext?.WriteLine($"[ERROR] Incorrect subject for fullUrl starting with {fullUrlPrefix}: Expected '{subject}', Found '{actualSubject}'");
                        throw new Exception($"Validation failed: subject mismatch at fullUrl starting with {fullUrlPrefix}");
                    }
                }
            }
            TestContext?.WriteLine("[PASS] All fullUrl entries and corresponding resourceType, id, and subject values are valid.");
        }
        public void ValidateSingleMeasureAdHocAggregateACHFile()
        {
            string fileName = "aggregate-ACH.json";

            var entry = _zipContents.Keys.FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                throw new Exception($"{fileName} is missing from the ZIP archive.");
            var content = _zipContents[entry];
            JObject json = JObject.Parse(content);
            int actualCount = (int?)json["group"]?[0]?["population"]?[0]?["count"] ?? -1;
            if (actualCount != 6)
            {
                TestContext?.WriteLine($"[ERROR] MeasureReport count mismatch: Expected 6, Found {actualCount}");
                throw new Exception("Validation failed: MeasureReport 'count' is incorrect.");
            }
            string? measureValue = (string?)json["measure"];
            if (string.IsNullOrWhiteSpace(measureValue) || !measureValue.Contains("|"))
            {
                TestContext?.WriteLine($"[ERROR] MeasureReport 'measure' value is missing or malformed: '{measureValue}'");
                throw new Exception("Validation failed: MeasureReport 'measure' field is missing or malformed.");
            }
            string version = measureValue.Split('|').Last();
            if (version != singleMeasureAdHocACHdQMVersion)
            {
                TestContext?.WriteLine($"[ERROR] MeasureReport version mismatch: Expected '{singleMeasureAdHocACHdQMVersion}', Found '{version}'");
                throw new Exception("Validation failed: MeasureReport 'measure' version is incorrect.");
            }
            TestContext?.WriteLine($"[PASS] aggregate-ACH.json: 'count' == 6 and 'measure' version == '{singleMeasureAdHocACHdQMVersion}'.");
        }
        public async Task WaitForSingleMeasureZipContentsAsync(int timeoutInSeconds = 180, List<string>? requiredFiles = null)
        {
            DateTime endTime = DateTime.UtcNow.AddSeconds(timeoutInSeconds);
            while (DateTime.UtcNow < endTime)
            {
                try
                {
                    await DownloadAndExtractSingleMeasureZipAsync();

                    var jsonFiles = _zipContents.Keys
                        .Where(name => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (requiredFiles == null && jsonFiles.Count > 0)
                    {
                        TestContext?.WriteLine("[INFO] ZIP contents are now available.");
                        return;
                    }
                    if (requiredFiles != null)
                    {
                        bool allPresent = requiredFiles.All(req =>
                            _zipContents.Keys.Any(actual => actual.EndsWith(req, StringComparison.OrdinalIgnoreCase)));

                        if (allPresent)
                        {
                            TestContext?.WriteLine("[INFO] All required files were found in the ZIP archive.");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    TestContext?.WriteLine($"[INFO] Waiting for ZIP contents... ");
                }
                await Task.Delay(1000);
            }
            throw new TimeoutException($"ZIP contents were not available within {timeoutInSeconds} seconds.");
        }
    }
}

