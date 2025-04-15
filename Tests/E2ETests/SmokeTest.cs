using System.Net;
using System.Runtime.InteropServices.JavaScript;
using Newtonsoft.Json.Linq;

namespace LantanaGroup.Link.Tests.E2ETests;

using RestSharp;

[TestClass]
public sealed class SmokeTest
{
    private const string FacilityId = "smoke-test-facility";
    private static readonly RestClient AdminBffClient = new RestClient(TestConfig.AdminBffBase);
    private static readonly FhirDataLoader FhirDataLoader = new FhirDataLoader(TestConfig.FhirServerBase);
    
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        // Load data onto FHIR server
        await FhirDataLoader.LoadEmbeddedTransactionBundles();
        
        // Initialize validation artifacts and categories
        await InitializeValidationArtifacts();
        await InitializeValidationCategories();
    }
    
    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        // Clear all data from the FHIR server
        FhirDataLoader.DeleteResourcesWithExpunge();
        
        // Cleanup
        await DeleteFacility();
    }
    
    [TestMethod]
    public async Task ExecuteSmokeTest()
    {
        // Get and load measure definition into measureeval and validation
        var measureLoader = new MeasureLoader(AdminBffClient);
        await measureLoader.LoadAsync();
        
        await this.CreateFacilityAsync(measureLoader.measureId);
        
        await this.CreateNormalizationConfig();
        
        await this.CreateQueryPlan(measureLoader.measureId, "Epic");

        await this.CreateQueryConfig();
        
        await this.GenerateReport(measureLoader.measureId);
    }

    private async Task GenerateReport(string measureId)
    {
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
        Assert.IsTrue(response.StatusCode == System.Net.HttpStatusCode.OK, $"Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}");
    }

    private static async Task InitializeValidationArtifacts()
    {
        Console.WriteLine("Initializing validation artifacts...");
        var request = new RestRequest("validation/artifact/$initialize", Method.Post);
        var response = AdminBffClient.Execute(request);
        Assert.IsTrue(response.StatusCode == System.Net.HttpStatusCode.OK, $"Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}");
    }

    private static async Task InitializeValidationCategories()
    {
        Console.WriteLine("Initializing validation categories...");
        var request = new RestRequest("validation/category/$initialize", Method.Post);
        var response = AdminBffClient.Execute(request);
        Assert.IsTrue(response.StatusCode == System.Net.HttpStatusCode.OK, $"Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}");
    }

    private async Task<RestResponse> CreateFacilityAsync(string measure)
    {
        Console.WriteLine("Creating facility...");
        var request = new RestRequest("/Facility", Method.Post);
        request.AddHeader("Content-Type", "application/json");

        var body = new
        {
            FacilityId = FacilityId,
            FacilityName = FacilityId,
            TimeZone = "America/Chicago",
            ScheduledReports = new
            {
                monthly = new[] { measure },
                daily = Array.Empty<string>(),
                weekly = Array.Empty<string>()
            }
        };

        request.AddJsonBody(body);

        var response = await AdminBffClient.ExecuteAsync(request);
        
        Assert.IsTrue(response.StatusCode == System.Net.HttpStatusCode.Created, "Expected HTTP 201 Created for facility creation");
        
        return response;
    }

    private async Task CreateNormalizationConfig()
    {
        Console.WriteLine("Creating normalization config...");
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
        Assert.IsTrue(response.StatusCode == System.Net.HttpStatusCode.Created, $"Response was not 200 OK {response.StatusCode}: {response.Content}");
    }

    private async Task CreateQueryConfig()
    {
        Console.WriteLine("Creating query config...");
        var request = new RestRequest($"data/fhirQueryConfiguration", Method.Post);
        var body = new JObject
        {
            ["FacilityId"] = FacilityId,
            ["FhirServerBaseUrl"] = TestConfig.FhirServerBase
        };
        request.AddJsonBody(body.ToString(), "application/json");
        
        var response = await AdminBffClient.ExecuteAsync(request);
        Assert.IsTrue(response.StatusCode == HttpStatusCode.Created, $"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
    }

    private async Task CreateQueryPlan(string measureId, string ehrDescription)
    {
        Console.WriteLine("Creating query plan...");
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
        Assert.IsTrue(response.StatusCode == HttpStatusCode.Created, $"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
    }
    
    #region Delete Facility Methods

    private static async Task DeleteFacility()
    {
        await Task.WhenAll(
            DeleteFacilityNormalization(),
            DeleteFacilityQueryPlan(),
            DeleteFacilityQueryConfig()
        );
        
        Console.WriteLine("Deleting facility...");
        var deleteFacilityRequest = new RestRequest($"/Facility/{FacilityId}", Method.Delete);
        var deleteFacilityResponse = await AdminBffClient.ExecuteAsync(deleteFacilityRequest);

        if (deleteFacilityResponse.StatusCode != HttpStatusCode.NoContent)
            Console.WriteLine($"Expected HTTP 204 No Content for facility deletion but received {deleteFacilityResponse.StatusCode}: {deleteFacilityResponse.Content}");
    }

    private static async Task DeleteFacilityNormalization()
    {
        Console.WriteLine("Deleting facility normalization...");
        var deleteNormalizationRequest = new RestRequest($"/normalization/{FacilityId}", Method.Delete);
        var deleteNormalizationResponse = await AdminBffClient.ExecuteAsync(deleteNormalizationRequest);

        if (deleteNormalizationResponse.StatusCode != HttpStatusCode.Accepted)
            Console.WriteLine($"Expected HTTP 204 No Content for normalization deletion but received {deleteNormalizationResponse.StatusCode}: {deleteNormalizationResponse.Content}");
    }

    private static async Task DeleteFacilityQueryPlan()
    {
        Console.WriteLine("Deleting facility query plan...");
        var deleteQueryPlanRequest = new RestRequest($"/data/{FacilityId}/QueryPlan", Method.Delete);
        var deleteQueryPlanResponse = await AdminBffClient.ExecuteAsync(deleteQueryPlanRequest);

        if (deleteQueryPlanResponse.StatusCode != HttpStatusCode.Accepted)
            Console.WriteLine($"Expected HTTP 204 No Content for query plan deletion but received {deleteQueryPlanResponse.StatusCode}: {deleteQueryPlanResponse.Content}");
    }

    private static async Task DeleteFacilityQueryConfig()
    {
        Console.WriteLine("Deleting facility query config...");
        var deleteQueryConfigRequest = new RestRequest($"/data/{FacilityId}/fhirQueryConfiguration", Method.Delete);
        var deleteQueryConfigResponse = await AdminBffClient.ExecuteAsync(deleteQueryConfigRequest);

        if (deleteQueryConfigResponse.StatusCode != HttpStatusCode.Accepted)
            Console.WriteLine($"Expected HTTP 204 No Content for query config deletion but received {deleteQueryConfigResponse.StatusCode}: {deleteQueryConfigResponse.Content}");
    }
    
    #endregion
}