using System.Net;
using Newtonsoft.Json.Linq;

namespace LantanaGroup.Link.Tests.E2ETests;

using RestSharp;

[TestClass]
public sealed class SmokeTest
{
    private static readonly string facilityId = "smoke-test-facility";
    private static RestClient adminBffClient = new RestClient(TestConfig.AdminBffBase);
    private static FhirDataLoader fhirDataLoader = new FhirDataLoader(TestConfig.FhirServerBase);
    
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        // Load data onto FHIR server
        fhirDataLoader.LoadEmbeddedTransactionBundles();
    }
    
    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        // Clear all data from the FHIR server
        fhirDataLoader.DeleteResourcesWithExpunge();
        
        // Cleanup
        await DeleteFacility();
    }
    
    [TestMethod]
    public async Task ExecuteSmokeTest()
    {
        // Get and load measure definition into measureeval and validation
        MeasureLoader measureLoader = new MeasureLoader(adminBffClient);
        await measureLoader.LoadAsync();
        
        await this.CreateFacilityAsync(measureLoader.measureId);
        
        await this.CreateNormalizationConfig();
        
        await this.CreateQueryPlan(measureLoader.measureId, "Epic");

        await this.CreateQueryConfig();
    }

    public async Task<RestResponse> CreateFacilityAsync(string measure)
    {
        Console.WriteLine("Creating facility...");
        var request = new RestRequest("/Facility", Method.Post);
        request.AddHeader("Content-Type", "application/json");

        var body = new
        {
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = "America/Chicago",
            ScheduledReports = new
            {
                monthly = new[] { measure },
                daily = Array.Empty<string>(),
                weekly = Array.Empty<string>()
            }
        };

        request.AddJsonBody(body);

        var response = await adminBffClient.ExecuteAsync(request);
        
        Assert.IsTrue(response.StatusCode == System.Net.HttpStatusCode.Created, "Expected HTTP 201 Created for facility creation");
        
        return response;
    }

    private async Task CreateNormalizationConfig()
    {
        Console.WriteLine("Creating normalization config...");
        var request = new RestRequest("normalization", Method.Post);
        string conceptMapJson = TestConfig.GetEmbeddedResourceContent("LantanaGroup.Link.Tests.E2ETests.test_data.smoke_test.concept-map.json");

        // Construct the request body with dynamic facilityId
        var body = new JObject
        {
            ["FacilityId"] = facilityId,
            ["OperationSequence"] = new JObject
            {
                ["0"] = new JObject
                {
                    ["$type"] = "ConceptMapOperation",
                    ["FacilityId"] = facilityId,
                    ["name"] = $"{facilityId} Concept Map example",
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
                    ["facilityId"] = facilityId,
                    ["name"] = "PeriodDateFixer",
                    ["conditions"] = new JArray(),
                    ["transformResource"] = "",
                    ["transformElement"] = "Period",
                    ["transformValue"] = ""
                },
                ["3"] = new JObject
                {
                    ["$type"] = "ConditionalTransformationOperation",
                    ["facilityId"] = facilityId,
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
        var response = await adminBffClient.ExecuteAsync(request);
        Assert.IsTrue(response.StatusCode == System.Net.HttpStatusCode.Created, $"Response was not 200 OK {response.StatusCode}: {response.Content}");
    }

    private async Task CreateQueryConfig()
    {
        Console.WriteLine("Creating query config...");
        var request = new RestRequest($"data/fhirQueryConfiguration", Method.Post);
        var body = new JObject
        {
            ["FacilityId"] = facilityId,
            ["FhirServerBaseUrl"] = TestConfig.FhirServerBase
        };
        request.AddJsonBody(body.ToString(), "application/json");
        
        var response = await adminBffClient.ExecuteAsync(request);
        Assert.IsTrue(response.StatusCode == HttpStatusCode.Created, $"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
    }

    private async Task CreateQueryPlan(string measureId, string ehrDescription)
    {
        Console.WriteLine("Creating query plan...");
        var request = new RestRequest($"data/{facilityId}/QueryPlan", Method.Post);

        var body = new JObject
        {
            ["PlanName"] = measureId,
            ["ReportType"] = measureId,
            ["FacilityId"] = facilityId,
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

        var response = await adminBffClient.ExecuteAsync(request);
        Assert.IsTrue(response.StatusCode == HttpStatusCode.Created, $"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
    }

    private static async Task DeleteFacility()
    {
        await Task.WhenAll(
            DeleteFacilityNormalization(),
            DeleteFacilityQueryPlan(),
            DeleteFacilityQueryConfig()
        );
        
        Console.WriteLine("Deleting facility...");
        var deleteFacilityRequest = new RestRequest($"/Facility/{facilityId}", Method.Delete);
        var deleteFacilityResponse = await adminBffClient.ExecuteAsync(deleteFacilityRequest);

        if (deleteFacilityResponse.StatusCode != HttpStatusCode.NoContent)
            Console.WriteLine($"Expected HTTP 204 No Content for facility deletion but received {deleteFacilityResponse.StatusCode}: {deleteFacilityResponse.Content}");
    }

    private static async Task DeleteFacilityNormalization()
    {
        Console.WriteLine("Deleting facility normalization...");
        var deleteNormalizationRequest = new RestRequest($"/normalization/{facilityId}", Method.Delete);
        var deleteNormalizationResponse = await adminBffClient.ExecuteAsync(deleteNormalizationRequest);

        if (deleteNormalizationResponse.StatusCode != HttpStatusCode.Accepted)
            Console.WriteLine($"Expected HTTP 204 No Content for normalization deletion but received {deleteNormalizationResponse.StatusCode}: {deleteNormalizationResponse.Content}");
    }

    private static async Task DeleteFacilityQueryPlan()
    {
        
        Console.WriteLine("Deleting facility query plan...");
        var deleteQueryPlanRequest = new RestRequest($"/data/{facilityId}/QueryPlan", Method.Delete);
        var deleteQueryPlanResponse = await adminBffClient.ExecuteAsync(deleteQueryPlanRequest);

        if (deleteQueryPlanResponse.StatusCode != HttpStatusCode.Accepted)
            Console.WriteLine($"Expected HTTP 204 No Content for query plan deletion but received {deleteQueryPlanResponse.StatusCode}: {deleteQueryPlanResponse.Content}");
    }

    private static async Task DeleteFacilityQueryConfig()
    {
        Console.WriteLine("Deleting facility query config...");
        var deleteQueryConfigRequest = new RestRequest($"/data/{facilityId}/fhirQueryConfiguration", Method.Delete);
        var deleteQueryConfigResponse = await adminBffClient.ExecuteAsync(deleteQueryConfigRequest);

        if (deleteQueryConfigResponse.StatusCode != HttpStatusCode.Accepted)
            Console.WriteLine($"Expected HTTP 204 No Content for query config deletion but received {deleteQueryConfigResponse.StatusCode}: {deleteQueryConfigResponse.Content}");
    }
}