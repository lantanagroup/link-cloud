using Xunit.Abstractions;
using Hl7.Fhir.Model;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;
using Xunit;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.Tests.BackendE2ETests.ApiRequests;
using System.Diagnostics;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class AdhocReportingSmokeTest(ITestOutputHelper output) : IAsyncLifetime
{
    private const string FacilityId = "SmokeTestFacility";
    private const int PollingIntervalSeconds = 5;
    private const int MaxRetryCount = 100;
    private static readonly RestClient AdminBffClient = new RestClient(TestConfig.AdminBffBase);
    private static readonly FhirDataLoader FhirDataLoader = new FhirDataLoader(TestConfig.ExternalFhirServerBase);

    public async Task InitializeAsync()
    {
        if (TestConfig.AdminBffOAuth.ShouldAuthenticate)
        {
            // Get a token for the user
            string token = AuthHelper.GetBearerToken(TestConfig.AdminBffOAuth);

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Could not get token for user");

            AdminBffClient.AddDefaultHeader("Authorization", "Bearer " + token);
        }

        //Load data onto FHIR server
        await FhirDataLoader.LoadEmbeddedTransactionBundles(output);
        // Initialize validation artifacts and categories
        await InitializeValidationArtifacts();
        await InitializeValidationCategories();
    }

    public async Task DisposeAsync()
    {
        output.WriteLine("Cleaning up...\n");

        if (TestConfig.AdhocReportingSmokeTestConfig.RemoveFacilityConfig)
        {
            await DeleteFacility();
        }

        // Clear all data from the FHIR server
        if (TestConfig.CleanupSmokeTestData)
            FhirDataLoader.ExpungeEverything(output);

        if (TestConfig.AdhocReportingSmokeTestConfig.RemoveReport)
        {
            // TODO: Delete report
        }
    }

    [Fact]
    [Trait("Category", "FhirAndTenantConfigLoadOnly")]
    public async Task LoadConfigAndPatientFhirData()
    {
        TestConfig.AdhocReportingSmokeTestConfig.RemoveFacilityConfig = false;
        AdHocReportApiRequests apiE2E = new AdHocReportApiRequests(output);
        var measureLoader = new MeasureLoader(AdminBffClient, output);

        await measureLoader.LoadAsync();
        apiE2E.Create_SingleMeasureAdHocTestFacility();
        apiE2E.Create_SingleMeasureCensusConfiguration_AdHoc();
        apiE2E.Create_SingleMeasureQueryDispatchConfig_AdHoc();
        apiE2E.Create_SingleMeasure_FHIRQueryConfigByFacility_AdHoc();
        apiE2E.Create_SingleMeasure_MontlhyQueryPlanByFacility_AdHoc();
        apiE2E.Create_SingleMeasure_DischargeQueryPlanByFacility_AdHoc();
        apiE2E.Create_SingleMeasureFHIRQueryListByFacility_AdHoc();
    }

    [Fact]
    [Trait("Category", "SmokeTest")]
    public async Task ExecuteSmokeTest()
    {
        // Get and load measure definition into measureeval and validation
        var measureLoader = new MeasureLoader(AdminBffClient, output);
        await measureLoader.LoadAsync();

        await this.CreateFacilityAsync(measureLoader.MeasureId);

        await this.CreateNormalizationConfig();

        await this.CreateQueryPlan(measureLoader.MeasureId, "Epic");

        await this.CreateQueryConfig();

        await this.GenerateReport(measureLoader.MeasureId);
    }

    [Fact]
    [Trait("Category", "AdHocSingleMeasureSmokeTest")]
    public async Task SmokeTest_GenerateSingleMeasureAdHocReport()
    {        
        TestConfig.AdhocReportingSmokeTestConfig.RemoveFacilityConfig = true;
        AdHocReportApiRequests apiE2E = new AdHocReportApiRequests(output);
        SubmissionZipReader submissionReportZip = new SubmissionZipReader(output);
        AdhocReportingSmokeTest adhocReportingSmokeTest = new AdhocReportingSmokeTest(output);
        MeasureLoader measureLoader = new MeasureLoader(AdminBffClient, output);

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        output.WriteLine($"Stopwatch start {DateTime.UtcNow.ToString()}");

        await measureLoader.LoadAsync();
        apiE2E.Create_SingleMeasureAdHocTestFacility();
        apiE2E.Create_SingleMeasureCensusConfiguration_AdHoc();
        apiE2E.Create_SingleMeasureQueryDispatchConfig_AdHoc();
        apiE2E.Create_SingleMeasure_FHIRQueryConfigByFacility_AdHoc();
        apiE2E.Create_SingleMeasure_MontlhyQueryPlanByFacility_AdHoc();
        apiE2E.Create_SingleMeasure_DischargeQueryPlanByFacility_AdHoc();
        apiE2E.Create_SingleMeasureFHIRQueryListByFacility_AdHoc();
        apiE2E.GenerateSingleMeasureAdHocReport_ACH();

        await submissionReportZip.WaitForSingleMeasureZipContentsAsync();
        var failures = new List<string>();
        try
        {
            await submissionReportZip.DownloadAndExtractSingleMeasureZipAsync(true);
            TestConfig.ValidationHelper.TryRunValidation(submissionReportZip.SingleMeasureAdHocValidateFilesAppear, failures);
            TestConfig.ValidationHelper.TryRunValidation(submissionReportZip.SingleMeasureAdHocValidateFilesDoNotAppear, failures);
            TestConfig.ValidationHelper.TryRunValidation(() => submissionReportZip.ValidateSpecificPatientFileContents(3, 2000), failures);
            TestConfig.ValidationHelper.TryRunValidation(submissionReportZip.ValidateSingleMeasureAdHocAggregateACHMFile, failures);
            apiE2E.GETSingleMeasureAdHocFacilityValidationResultsForReport();
        }
        finally
        {
            if (failures.Any())
            {
                output.WriteLine("🔴 ================= TEST RESULT SUMMARY =================🔴 ");
                foreach (var fail in failures)
                    output.WriteLine(fail);
                Xunit.Assert.Fail($"{failures.Count} verification(s) failed. See console output below.");
            }
            else
                output.WriteLine("[PASS] Smoke test completed with all verifications passing.");

            stopwatch.Stop();
            output.WriteLine($"Stopwatch stop {DateTime.UtcNow.ToString()} - Total Time: {stopwatch.Elapsed}");
        }      
    }

    private async Task GenerateReport(string? measureId)
    {
        if (measureId == null)
            throw new ArgumentNullException(nameof(measureId));

        var request = new RestRequest($"facility/{FacilityId}/AdhocReport", Method.Post);
        var body = new
        {
            BypassSubmission = false,
            TestConfig.AdhocReportingSmokeTestConfig.StartDate,
            TestConfig.AdhocReportingSmokeTestConfig.EndDate,
            ReportTypes = new[] { measureId },
            TestConfig.AdhocReportingSmokeTestConfig.PatientIds
        };
        request.AddJsonBody(body);

        var response = await AdminBffClient.ExecuteAsync(request);
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, $"Generate Report - Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}");

        Assert.True(response.ContentType != null, $"Expected Content-Type to be set but received {response.ContentType}");
        Assert.True(response.ContentType.Contains("application/json"), $"Expected Content-Type to be application/json but received {response.ContentType}");
        Assert.False(string.IsNullOrWhiteSpace(response.Content), $"Expected Content to be set but received {response.Content}");

        var generateReportResponse = JObject.Parse(response.Content);

        Assert.True(generateReportResponse.ContainsKey("reportId"), $"Expected response to include ReportId but received {generateReportResponse}");

        var reportId = generateReportResponse["reportId"]?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(reportId), $"Expected ReportId to be set but received {reportId}");

        var reportSubmitted = await this.CheckReportSubmissionStatusAsync(FacilityId, reportId);
        Assert.True(reportSubmitted, $"Expected report with id {reportId} to be submitted but it was not");

        var downloadedResources = await this.DownloadReport(reportId);

        // Confirm that there is a file called "manifest.ndjson"
        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"), $"Expected report to include manifest.ndjson but it was not");
        // TODO: Validate that it is correct

        // Confirm that there is a file called "patient-{patientId}.ndjson"
        foreach (var patientId in TestConfig.AdhocReportingSmokeTestConfig.PatientIds)
        {
            Assert.True(downloadedResources.ContainsKey($"patient-{patientId}.ndjson"), $"Expected report to include patient-{patientId}.ndjson but it was not");
            // TODO: Validate that it is correct
        }

        output.WriteLine("Done generating and validating report.");
    }
    private async Task<Dictionary<string, Object>> DownloadReport(string reportId)
    {
        var request = new RestRequest($"submission/{FacilityId}/{reportId}", Method.Get);
        var response = await AdminBffClient.ExecuteAsync(request);
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, $"Download Report - Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}");

        Assert.True(response.ContentType?.Contains("application/zip"), $"Expected Content-Type to be application/zip but received {response.ContentType}");

        var responseDictionary = new Dictionary<string, Object>();

        if (!string.IsNullOrEmpty(TestConfig.SmokeTestDownloadPath) && response.RawBytes != null)
        {
            if (!Directory.Exists(TestConfig.SmokeTestDownloadPath))
                Directory.CreateDirectory(TestConfig.SmokeTestDownloadPath);

            var downloadPath = Path.Combine(TestConfig.SmokeTestDownloadPath, "adhoc-reporting-smoke-test-submission.zip");
            await File.WriteAllBytesAsync(downloadPath, response.RawBytes);
            output.WriteLine($"Report downloaded to {downloadPath}");
        }

        using var zipStream = new MemoryStream(response.RawBytes ?? []);
        using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);
        var jsonParser = new Hl7.Fhir.Serialization.FhirJsonParser();

        foreach (var entry in archive.Entries)
        {
            if (entry.Length == 0)
                continue;

            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);
            var fileContent = reader.ReadToEnd();

            if (entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var resource = jsonParser.Parse<Resource>(fileContent);
                responseDictionary[entry.FullName] = resource;
            }
            else
            {
                responseDictionary[entry.FullName] = fileContent;
            }
        }

        return responseDictionary;
    }
    private async Task InitializeValidationArtifacts()
    {
        output.WriteLine("Initializing validation artifacts...");
        await RetryUntilSuccess(async () =>
        {
            var request = new RestRequest("validation/artifact/$initialize", Method.Post);
            request.Timeout = TimeSpan.FromMinutes(5.0);
            var response = await AdminBffClient.ExecuteAsync(request);
            Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK,
                $"Please Reset Your Docker Environment and ReRun. Initialize Validation Artifacts - Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}.");
        }, TimeSpan.FromMinutes(5.0), TimeSpan.FromMinutes(5.0));
    }
    private async Task InitializeValidationCategories()
    {
        output.WriteLine("Initializing validation categories...");
        await RetryUntilSuccess(async () =>
        {
            var request = new RestRequest("validation/category/$initialize", Method.Post);
            request.Timeout = TimeSpan.FromMinutes(5.0);
            var response = await AdminBffClient.ExecuteAsync(request);
            Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK,
                $"Please Reset Your Docker Environment and ReRun. Initialize Validation Categories - Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}.");
        }, TimeSpan.FromMinutes(5.0), TimeSpan.FromMinutes(5.0));
    }
    private async Task<RestResponse> CreateFacilityAsync(string? measure)
    {
        output.WriteLine("Creating facility...");
        var request = new RestRequest("/Facility", Method.Post);
        request.AddHeader("Content-Type", "application/json");

        var body = new
        {
            FacilityId = FacilityId,
            FacilityName = FacilityId,
            TimeZone = "America/Chicago",
            ScheduledReports = new
            {
                monthly = measure != null ? new[] { measure } : Array.Empty<string>(),
                daily = Array.Empty<string>(),
                weekly = Array.Empty<string>()
            }
        };

        request.AddJsonBody(body);

        var response = await AdminBffClient.ExecuteAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Created, $"Expected HTTP 201 Created for facility creation but got {response.StatusCode}");

        return response;
    }
    private async Task CreateNormalizationConfig()
    {
        output.WriteLine("Creating normalization config...");
        var request = new RestRequest("normalization/Operations", Method.Post);

        // Construct the request body with dynamic facilityId
        var body = new
        {
            ResourceTypes = new[] { "Location" },
            FacilityId,
            Operation = new
            {
                OperationType = "CopyProperty",
                Name = "Copy Location Identifier to Type",
                Description = "A Test Operation",
                SourceFhirPath = "identifier.value",
                TargetFhirPath = "type[0].coding.code"
            },
            Description = "Copy Location Identifier to Code",
            VendorIds = Array.Empty<string>()
        };

        // Add the body to the request
        request.AddJsonBody(body);

        // Execute and assert
        var response = await AdminBffClient.ExecuteAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Response was not 201 Created {response.StatusCode}: {response.Content}");
    }

    private async Task CreateQueryConfig()
    {
        output.WriteLine("Creating query config...");
        var request = new RestRequest($"data/fhirQueryConfiguration", Method.Post);
        var body = new JObject
        {
            ["FacilityId"] = FacilityId,
            ["FhirServerBaseUrl"] = TestConfig.InternalFhirServerBase,
            ["MaxConcurrentRequests"] = TestConfig.FhirQueryConfig.MaxConcurrentRequests
        };
        request.AddJsonBody(body.ToString(), "application/json");
        var response = await AdminBffClient.ExecuteAsync(request);
        if (response.StatusCode != HttpStatusCode.Created)
            output.WriteLine($"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
    }

    private async Task CreateQueryPlan(string? measureId, string ehrDescription)
    {
        output.WriteLine("Creating query plan...");
        var request = new RestRequest($"data/{FacilityId}/QueryPlan", Method.Post);
        var body = new JObject
        {
            ["PlanName"] = measureId,
            ["FacilityId"] = FacilityId,
            ["EHRDescription"] = ehrDescription,
            ["LookBack"] = "P0D",
            ["Type"] = "Discharge",
            ["InitialQueries"] = new JObject
            {
                ["0"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Encounter",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 1,
                        ["Format"] = "ge{0}"
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 3,
                        ["Format"] = "le{0}"
                    }
                }
                },
                ["1"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "MedicationRequest",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    }
                }
                },
                ["2"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Location",
                    ["OperationType"] = 1,
                    ["Paged"] = 100
                },
                ["3"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Medication",
                    ["OperationType"] = 1,
                    ["Paged"] = 100
                }
            },
            ["SupplementalQueries"] = new JObject
            {
                ["0"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Condition",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.ResourceIdsParameter, DataAcquisition.Domain",
                        ["Name"] = "encounter",
                        ["Resource"] = "Encounter",
                        ["Paged"] = "100"
                    }
                }
                },
                ["1"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Coverage",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    }
                }
                },
                ["2"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "DiagnosticReport",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 1,
                        ["Format"] = "ge{0}"
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 3,
                        ["Format"] = "le{0}"
                    }
                }
                },
                ["3"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Observation",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 1,
                        ["Format"] = "ge{0}"
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 3,
                        ["Format"] = "le{0}"
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.LiteralParameter, DataAcquisition.Domain",
                        ["Name"] = "category",
                        ["Literal"] = "imaging,laboratory,social-history,vital-signs"
                    }
                }
                },
                ["4"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Procedure",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 1,
                        ["Format"] = "ge{0}"
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 3,
                        ["Format"] = "le{0}"
                    }
                }
                },
                ["5"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "ServiceRequest",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.ResourceIdsParameter, DataAcquisition.Domain",
                        ["Name"] = "encounter",
                        ["Resource"] = "Encounter",
                        ["Paged"] = "100"
                    }
                }
                },
                ["6"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Device",
                    ["OperationType"] = 1,
                    ["Paged"] = 100
                },
                ["7"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Specimen",
                    ["OperationType"] = 1,
                    ["Paged"] = 100
                }
            }
        };
        request.AddJsonBody(body.ToString(), "application/json");
        var response = await AdminBffClient.ExecuteAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
        body = new JObject
        {
            ["PlanName"] = measureId,
            ["FacilityId"] = FacilityId,
            ["EHRDescription"] = ehrDescription,
            ["LookBack"] = "P0D",
            ["Type"] = "Monthly",
            ["InitialQueries"] = new JObject
            {
                ["0"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Encounter",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 1,
                        ["Format"] = "ge{0}"
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 3,
                        ["Format"] = "le{0}"
                    }
                }
                },
                ["1"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "MedicationRequest",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    }
                }
                },
                ["2"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Location",
                    ["OperationType"] = 1,
                    ["Paged"] = 100
                },
                ["3"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Medication",
                    ["OperationType"] = 1,
                    ["Paged"] = 100
                }
            },
            ["SupplementalQueries"] = new JObject
            {
                ["0"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Condition",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.ResourceIdsParameter, DataAcquisition.Domain",
                        ["Name"] = "encounter",
                        ["Resource"] = "Encounter",
                        ["Paged"] = "100"
                    }
                }
                },
                ["1"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Coverage",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    }
                }
                },
                ["2"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "DiagnosticReport",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 1,
                        ["Format"] = "ge{0}"
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 3,
                        ["Format"] = "le{0}"
                    }
                }
                },
                ["3"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Observation",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 1,
                        ["Format"] = "ge{0}"
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 3,
                        ["Format"] = "le{0}"
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.LiteralParameter, DataAcquisition.Domain",
                        ["Name"] = "category",
                        ["Literal"] = "imaging,laboratory,social-history,vital-signs"
                    }
                }
                },
                ["4"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Procedure",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 1,
                        ["Format"] = "ge{0}"
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "date",
                        ["Variable"] = 3,
                        ["Format"] = "le{0}"
                    }
                }
                },
                ["5"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "ServiceRequest",
                    ["Parameters"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                        ["Name"] = "patient",
                        ["Variable"] = 0,
                        ["Format"] = null
                    },
                    new JObject
                    {
                        ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter.ResourceIdsParameter, DataAcquisition.Domain",
                        ["Name"] = "encounter",
                        ["Resource"] = "Encounter",
                        ["Paged"] = "100"
                    }
                }
                },
                ["6"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Device",
                    ["OperationType"] = 1,
                    ["Paged"] = 100
                },
                ["7"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Specimen",
                    ["OperationType"] = 1,
                    ["Paged"] = 100
                }
            }
        };
        request = new RestRequest($"data/{FacilityId}/QueryPlan", Method.Post);
        request.AddJsonBody(body.ToString(), "application/json");
        response = await AdminBffClient.ExecuteAsync(request);
        if (response.StatusCode != HttpStatusCode.Created)
            output.WriteLine($"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
    }

    #region Delete Facility Methods
    private async Task DeleteFacility()
    {
        await Task.WhenAll(
            DeleteFacilityNormalization(),
            DeleteFacilityQueryPlan(),
            DeleteFacilityQueryConfig()
        );

        output.WriteLine("Deleting facility...");
        var deleteFacilityRequest = new RestRequest($"/Facility/{FacilityId}", Method.Delete);
        var deleteFacilityResponse = await AdminBffClient.ExecuteAsync(deleteFacilityRequest);

        if (deleteFacilityResponse.StatusCode != HttpStatusCode.NoContent)
            output.WriteLine($"Expected HTTP 204 No Content for facility deletion but received {deleteFacilityResponse.StatusCode}: {deleteFacilityResponse.Content}");
    }
    private async Task DeleteFacilityNormalization()
    {
        output.WriteLine("Deleting facility normalization...");
        var deleteNormalizationRequest = new RestRequest($"/normalization/operations/facility/{FacilityId}", Method.Delete);
        var deleteNormalizationResponse = await AdminBffClient.ExecuteAsync(deleteNormalizationRequest);

        if (deleteNormalizationResponse.StatusCode != HttpStatusCode.NoContent)
            output.WriteLine($"Expected HTTP 204 No Content for normalization deletion but received {deleteNormalizationResponse.StatusCode}: {deleteNormalizationResponse.Content}");
    }
    private async Task DeleteFacilityQueryPlan()
    {
        output.WriteLine("Deleting facility discharge query plan...");
        var deleteQueryPlanRequest = new RestRequest($"/data/{FacilityId}/QueryPlan?type=Discharge", Method.Delete);
        var deleteQueryPlanResponse = await AdminBffClient.ExecuteAsync(deleteQueryPlanRequest);

        if (deleteQueryPlanResponse.StatusCode != HttpStatusCode.Accepted)
            output.WriteLine($"Expected HTTP 202 Accepted for discharge query plan deletion but received {deleteQueryPlanResponse.StatusCode}: {deleteQueryPlanResponse.Content}");

        output.WriteLine("Deleting facility monthly query plan...");
        deleteQueryPlanRequest = new RestRequest($"/data/{FacilityId}/QueryPlan?type=Monthly", Method.Delete);
        deleteQueryPlanResponse = await AdminBffClient.ExecuteAsync(deleteQueryPlanRequest);

        if (deleteQueryPlanResponse.StatusCode != HttpStatusCode.Accepted)
            output.WriteLine($"Expected HTTP 202 Accepted for monthly query plan deletion but received {deleteQueryPlanResponse.StatusCode}: {deleteQueryPlanResponse.Content}");
    }
    private async Task DeleteFacilityQueryConfig()
    {
        output.WriteLine("Deleting facility query config...");
        var deleteQueryConfigRequest = new RestRequest($"/data/{FacilityId}/fhirQueryConfiguration", Method.Delete);
        var deleteQueryConfigResponse = await AdminBffClient.ExecuteAsync(deleteQueryConfigRequest);

        if (deleteQueryConfigResponse.StatusCode != HttpStatusCode.Accepted)
            output.WriteLine($"Expected HTTP 202 Accepted for query config deletion but received {deleteQueryConfigResponse.StatusCode}: {deleteQueryConfigResponse.Content}");
    }
    #endregion

    /// <summary>
    /// Asynchronously checks the submission status of a report.
    /// </summary>
    /// <param name="facilityId">The facility ID to query.</param>
    /// <param name="reportId">The report ID to match.</param>
    /// <returns>A Task<bool> indicating if the report is submitted.</returns>
    public async Task<bool> CheckReportSubmissionStatusAsync(string facilityId, string reportId)
    {
        output.WriteLine($"Checking report submission status for report {reportId}...");

        for (var retry = 0; retry < MaxRetryCount; retry++)
        {
            var request = new RestRequest($"/Report/summaries?facilityId={facilityId}", Method.Get);
            var response = await AdminBffClient.ExecuteAsync(request);

            if (response.StatusCode == HttpStatusCode.OK && response.ContentType != null && response.ContentType.Contains("application/json") && response.Content != null)
            {
                var jsonResponse = JObject.Parse(response.Content);
                var records = jsonResponse["records"] as JArray;

                if (records != null)
                {
                    var foundReport = records.FirstOrDefault(r => r["id"]?.ToString() == reportId);

                    if (foundReport == null)
                    {
                        output.WriteLine("Report not found, yet.");
                    }
                    else if (bool.Parse(foundReport["submitted"]?.ToString() ?? "false"))
                    {
                        output.WriteLine("Report submitted.");
                        return true;
                    }
                    else
                    {
                        output.WriteLine("Report not submitted, yet.");
                    }
                }
            }

            output.WriteLine($"Report is not submitted. Retrying in {PollingIntervalSeconds} seconds...");
            await Task.Delay(PollingIntervalSeconds * 1000);
        }
        output.WriteLine($"Report {reportId} was not submitted after {MaxRetryCount} retries.");
        return false;
    }
    private async Task RetryUntilSuccess(Func<Task> action, TimeSpan timeout, TimeSpan delay)
    {
        var start = DateTime.UtcNow;
        Exception lastException = null;

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(delay);
            }
        }

        throw new TimeoutException($"Operation failed after retrying for {timeout.TotalSeconds} seconds.", lastException);
    }
}