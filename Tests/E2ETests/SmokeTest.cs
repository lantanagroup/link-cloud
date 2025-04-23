using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests;

using System.Net;
using Hl7.Fhir.Model;
using Newtonsoft.Json.Linq;
using Xunit;
using Task = System.Threading.Tasks.Task;
using RestSharp;

public sealed class SmokeTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    
    private const string FacilityId = "smoke-test-facility";
    private const int PollingIntervalSeconds = 5;
    private const int MaxRetryCount = 15;
    private static readonly RestClient AdminBffClient = new RestClient(TestConfig.AdminBffBase);
    private static readonly FhirDataLoader FhirDataLoader = new FhirDataLoader(TestConfig.ExternalFhirServerBase);

    public SmokeTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    public async Task InitializeAsync()
    {
        // Load data onto FHIR server
        await FhirDataLoader.LoadEmbeddedTransactionBundles();

        // Initialize validation artifacts and categories
        await InitializeValidationArtifacts();
        await InitializeValidationCategories();
    }

    public async Task DisposeAsync()
    {
        // Clear all data from the FHIR server
        FhirDataLoader.DeleteResourcesWithExpunge();

        // TODO: Delete report

        // Cleanup
        await DeleteFacility();
    }

    [Fact]
    public async Task ExecuteSmokeTest()
    {
        // Get and load measure definition into measureeval and validation
        var measureLoader = new MeasureLoader(AdminBffClient, _output);
        await measureLoader.LoadAsync();

        await this.CreateFacilityAsync(measureLoader.MeasureId);

        await this.CreateNormalizationConfig();

        await this.CreateQueryPlan(measureLoader.MeasureId, "Epic");

        await this.CreateQueryConfig();

        await this.GenerateReport(measureLoader.MeasureId);
    }

    private async Task GenerateReport(string? measureId)
    {
        if (measureId == null)
            throw new ArgumentNullException(nameof(measureId));
        
        var request = new RestRequest($"facility/{FacilityId}/AdhocReport", Method.Post);
        var body = new
        {
            BypassSubmission = false,
            StartDate = "2025-03-01T00:00:00Z",
            EndDate = "2025-03-24T23:59:59.99Z",
            ReportTypes = new[] { measureId },
            PatientIds = new[] { "Patient-ACHMarch1" }
        };
        request.AddJsonBody(body);
        
        var response = await AdminBffClient.ExecuteAsync(request);
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, $"Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}");
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, $"Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}");
        
        // Check that the response is JSON
        Assert.True(response.ContentType != null, $"Expected Content-Type to be set but received {response.ContentType}");
        Assert.True(response.ContentType.Contains("application/json"), $"Expected Content-Type to be application/json but received {response.ContentType}");
        Assert.False(string.IsNullOrWhiteSpace(response.Content), $"Expected Content to be set but received {response.Content}");
        
        var generateReportResponse = JObject.Parse(response.Content);
        
        // Check that the response includes a ReportId
        Assert.True(generateReportResponse.ContainsKey("reportId"), $"Expected response to include ReportId but received {generateReportResponse}");
        
        var reportId = generateReportResponse["reportId"]?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(reportId), $"Expected ReportId to be set but received {reportId}");
        
        var reportSubmitted = await this.CheckReportSubmissionStatusAsync(FacilityId, reportId);
        Assert.True(reportSubmitted, $"Expected report with id {reportId} to be submitted but it was not");
        
        // Download the report
        var downloadedResources = await this.DownloadReport(reportId);
        
        // Confirm that there is a file called "sending-org.json"
        Assert.True(downloadedResources.ContainsKey("sending-organization.json"), $"Expected report to include sending-org.json but it was not");
        // TODO: Validate that it is correct
        
        // Confirm that there is a file called "patient-list.json"
        Assert.True(downloadedResources.ContainsKey("patient-list.json"), $"Expected report to include patient-list.json but it was not");
        // TODO: Validate that it is correct
        
        // Confirm that there is a file called "sending-device.json"
        Assert.True(downloadedResources.ContainsKey("sending-device.json"), $"Expected report to include sending-device.json but it was not");
        // TODO: Validate that it is correct
        
        // Confirm that there is a file called "aggregate-HYPO.json"
        // TODO: This should actually be "aggregate-ACH.json"
        Assert.True(downloadedResources.ContainsKey("aggregate-HYPO.json"), $"Expected report to include aggregate-HYPO.json but it was not");
        // TODO: Validate that it is correct
        
        // Confirm that there is a file called "other-resources.json"
        Assert.True(downloadedResources.ContainsKey("other-resources.json"), $"Expected report to include other-resources.json but it was not");
        // TODO: Validate that it is correct
        
        // Confirm that there is a file called "patient-Patient-ACHMarch1.json"
        Assert.True(downloadedResources.ContainsKey($"patient-Patient-ACHMarch1.json"), $"Expected report to include patient-{reportId}.json but it was not");
        // TODO: Validate that it is correct
    }
    
    private async Task<Dictionary<string, Object>> DownloadReport(string reportId)
    {
        var request = new RestRequest($"submission/{FacilityId}/{reportId}", Method.Get);
        var response = await AdminBffClient.ExecuteAsync(request);
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, $"Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}");
        
        // Expect the response to be a ZIP archive
        Assert.True(response.ContentType?.Contains("application/zip"), $"Expected Content-Type to be application/zip but received {response.ContentType}");
        
        var responseDictionary = new Dictionary<string, Object>();
    
        // Open the ZIP archive and extract in memory
        using var zipStream = new MemoryStream(response.RawBytes ?? Array.Empty<byte>());
        using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);
        var jsonParser = new Hl7.Fhir.Serialization.FhirJsonParser();
    
        foreach (var entry in archive.Entries)
        {
            // Skip directories and non-JSON files
            if (entry.Length == 0)
                continue;
    
            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);
            var fileContent = reader.ReadToEnd();

            if (entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                // Parse the content as a FHIR resource and add to dictionary
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
        _output.WriteLine("Initializing validation artifacts...");
        var request = new RestRequest("validation/artifact/$initialize", Method.Post);
        var response = await AdminBffClient.ExecuteAsync(request);
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, $"Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}");
    }

    private async Task InitializeValidationCategories()
    {
        _output.WriteLine("Initializing validation categories...");
        var request = new RestRequest("validation/category/$initialize", Method.Post);
        var response = await AdminBffClient.ExecuteAsync(request);
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, $"Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}");
    }

    private async Task<RestResponse> CreateFacilityAsync(string? measure)
    {
        _output.WriteLine("Creating facility...");
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
        
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Created, "Expected HTTP 201 Created for facility creation");
        
        return response;
    }

    private async Task CreateNormalizationConfig()
    {
        _output.WriteLine("Creating normalization config...");
        var request = new RestRequest("normalization", Method.Post);
        var conceptMapJson = TestConfig.GetEmbeddedResourceContent("LantanaGroup.Link.Tests.E2ETests.test_data.smoke_test.concept-map.json");

        // Construct the request body with dynamic facilityId
        var body = new JObject
        {
            ["FacilityId"] = FacilityId,
            ["OperationSequence"] = new JObject
            {
                ["0"] = new JObject
                {
                    ["$type"] = "ConceptMapOperation",
                    ["FacilityId"] = FacilityId,
                    ["name"] = $"{FacilityId} Concept Map example",
                    ["FhirConceptMap"] = JObject.Parse(conceptMapJson),
                    ["FhirPath"] = null,
                    ["FhirContext"] = "Encounter"
                },
                ["1"] = new JObject
                {
                    ["$type"] = "CopyLocationIdentifierToTypeOperation",
                    ["name"] = "Test Location Type"
                },
                ["2"] = new JObject
                {
                    ["$type"] = "ConditionalTransformationOperation",
                    ["facilityId"] = FacilityId,
                    ["name"] = "PeriodDateFixer",
                    ["conditions"] = new JArray(),
                    ["transformResource"] = "",
                    ["transformElement"] = "Period",
                    ["transformValue"] = ""
                },
                ["3"] = new JObject
                {
                    ["$type"] = "ConditionalTransformationOperation",
                    ["facilityId"] = FacilityId,
                    ["name"] = "EncounterStatusTransformation",
                    ["conditions"] = new JArray(),
                    ["transformResource"] = "Encounter",
                    ["transformElement"] = "Status",
                    ["transformValue"] = ""
                }
            }
        };

        // Add the body to the request
        request.AddJsonBody(body.ToString(), "application/json");

        // Execute and assert
        var response = await AdminBffClient.ExecuteAsync(request);
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Created, $"Response was not 201 Created {response.StatusCode}: {response.Content}");
    }

    private async Task CreateQueryConfig()
    {
        _output.WriteLine("Creating query config...");
        var request = new RestRequest($"data/fhirQueryConfiguration", Method.Post);
        var body = new JObject
        {
            ["FacilityId"] = FacilityId,
            ["FhirServerBaseUrl"] = TestConfig.InternalFhirServerBase
        };
        request.AddJsonBody(body.ToString(), "application/json");
        
        var response = await AdminBffClient.ExecuteAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
    }

    private async Task CreateQueryPlan(string? measureId, string ehrDescription)
    {
        _output.WriteLine("Creating query plan...");
        var request = new RestRequest($"data/{FacilityId}/QueryPlan", Method.Post);

        var body = new JObject
        {
            ["PlanName"] = measureId,
            ["ReportType"] = measureId,
            ["FacilityId"] = FacilityId,
            ["EHRDescription"] = ehrDescription,
            ["LookBack"] = "P0D",
            ["InitialQueries"] = new JObject
            {
                ["0"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Encounter",
                    ["Parameters"] = new JArray
                    {
                        new JObject
                        {
                            ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                            ["Name"] = "patient",
                            ["Variable"] = 0,
                            ["Format"] = null
                        },
                        new JObject
                        {
                            ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                            ["Name"] = "date",
                            ["Variable"] = 1,
                            ["Format"] = "ge{0}"
                        },
                        new JObject
                        {
                            ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                            ["Name"] = "date",
                            ["Variable"] = 3,
                            ["Format"] = "le{0}"
                        }
                    }
                },
                ["1"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Location",
                    ["OperationType"] = 1,
                    ["Paged"] = 100
                }
            },
            ["SupplementalQueries"] = new JObject
            {
                ["0"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Condition",
                    ["Parameters"] = new JArray
                    {
                        new JObject
                        {
                            ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                            ["Name"] = "patient",
                            ["Variable"] = 0,
                            ["Format"] = null
                        },
                        new JObject
                        {
                            ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.ResourceIdsParameter, DataAcquisition.Domain",
                            ["Name"] = "encounter",
                            ["Resource"] = "Encounter",
                            ["Paged"] = "100"
                        }
                    }
                },
                ["1"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Coverage",
                    ["Parameters"] = new JArray
                    {
                        new JObject
                        {
                            ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                            ["Name"] = "patient",
                            ["Variable"] = 0,
                            ["Format"] = null
                        }
                    }
                },
                ["2"] = new JObject
                {
                    ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                    ["ResourceType"] = "Observation",
                    ["Parameters"] = new JArray
                    {
                        new JObject
                        {
                            ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                            ["Name"] = "patient",
                            ["Variable"] = 0,
                            ["Format"] = null
                        },
                        new JObject
                        {
                            ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                            ["Name"] = "date",
                            ["Variable"] = 1,
                            ["Format"] = "ge{0}"
                        },
                        new JObject
                        {
                            ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                            ["Name"] = "date",
                            ["Variable"] = 3,
                            ["Format"] = "le{0}"
                        },
                        new JObject
                        {
                            ["$type"] = "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.LiteralParameter, DataAcquisition.Domain",
                            ["Name"] = "category",
                            ["Literal"] = "imaging,laboratory,social-history,vital-signs"
                        }
                    }
                },
            }
        };

        request.AddJsonBody(body.ToString(), "application/json");

        var response = await AdminBffClient.ExecuteAsync(request);
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
        
        _output.WriteLine("Deleting facility...");
        var deleteFacilityRequest = new RestRequest($"/Facility/{FacilityId}", Method.Delete);
        var deleteFacilityResponse = await AdminBffClient.ExecuteAsync(deleteFacilityRequest);

        if (deleteFacilityResponse.StatusCode != HttpStatusCode.NoContent)
            _output.WriteLine($"Expected HTTP 204 No Content for facility deletion but received {deleteFacilityResponse.StatusCode}: {deleteFacilityResponse.Content}");
    }

    private async Task DeleteFacilityNormalization()
    {
        _output.WriteLine("Deleting facility normalization...");
        var deleteNormalizationRequest = new RestRequest($"/normalization/{FacilityId}", Method.Delete);
        var deleteNormalizationResponse = await AdminBffClient.ExecuteAsync(deleteNormalizationRequest);

        if (deleteNormalizationResponse.StatusCode != HttpStatusCode.Accepted)
            _output.WriteLine($"Expected HTTP 202 Accepted for normalization deletion but received {deleteNormalizationResponse.StatusCode}: {deleteNormalizationResponse.Content}");
    }

    private async Task DeleteFacilityQueryPlan()
    {
        _output.WriteLine("Deleting facility query plan...");
        var deleteQueryPlanRequest = new RestRequest($"/data/{FacilityId}/QueryPlan", Method.Delete);
        var deleteQueryPlanResponse = await AdminBffClient.ExecuteAsync(deleteQueryPlanRequest);

        if (deleteQueryPlanResponse.StatusCode != HttpStatusCode.Accepted)
            _output.WriteLine($"Expected HTTP 202 Accepted for query plan deletion but received {deleteQueryPlanResponse.StatusCode}: {deleteQueryPlanResponse.Content}");
    }

    private async Task DeleteFacilityQueryConfig()
    {
        _output.WriteLine("Deleting facility query config...");
        var deleteQueryConfigRequest = new RestRequest($"/data/{FacilityId}/fhirQueryConfiguration", Method.Delete);
        var deleteQueryConfigResponse = await AdminBffClient.ExecuteAsync(deleteQueryConfigRequest);

        if (deleteQueryConfigResponse.StatusCode != HttpStatusCode.Accepted)
            _output.WriteLine($"Expected HTTP 202 Accepted for query config deletion but received {deleteQueryConfigResponse.StatusCode}: {deleteQueryConfigResponse.Content}");
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
                        _output.WriteLine("Report not found, yet.");
                    else if (bool.Parse(foundReport["submitted"]?.ToString() ?? "false"))
                        return true;
                    else
                        _output.WriteLine("Report not submitted, yet.");
                }
            }

            _output.WriteLine($"Report {reportId} is not submitted. Retrying in {PollingIntervalSeconds} seconds...");
            await Task.Delay(PollingIntervalSeconds * 1000); // Wait for 5 seconds before the next retry.
        }

        _output.WriteLine($"Report {reportId} was not submitted after {MaxRetryCount} retries.");
        return false; // Return false if the loop completes without finding the submitted report.
    }
}