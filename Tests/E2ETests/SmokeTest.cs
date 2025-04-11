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
        await DeleteFacility(facilityId);
    }
    
    [TestMethod]
    public async Task ExecuteSmokeTest()
    {
        // Get and load measure definition into measureeval and validation
        MeasureLoader measureLoader = new MeasureLoader();
        await measureLoader.LoadAsync(TestConfig.AdminBffBase);
        
        // Create a facility
        await this.CreateFacilityAsync(measureLoader.measureId);
        
        // Create normalization config
        await this.CreateNormalizationConfig();
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

    public async Task CreateNormalizationConfig()
    {
        var request = new RestRequest("/api/normalization", Method.Post);

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
                    ["FhirConceptMap"] = JObject.Parse(@"{
                        ""resourceType"": ""ConceptMap"",
                        ""id"": ""ehr-test-epic-encounter-class"",
                        ""url"": ""https://nhsnlink.org/fhir/ConceptMap/ehr-test-epic-encounter-class"",
                        ""identifier"": {
                            ""system"": ""urn:ietf:rfc:3986"",
                            ""value"": ""urn:uuid:63cd62ee-033e-414c-9f58-3ca97b5ffc3b""
                        },
                        ""version"": ""20220728"",
                        ""name"": ""ehr-test-epic-encounter-class"",
                        ""title"": ""Ehr-test Epic Encounter Class ConceptMap"",
                        ""status"": ""draft"",
                        ""experimental"": true,
                        ""date"": ""2022-07-28"",
                        ""description"": ""A mapping between the Epic's Encounter class codes and HL7 v3-ActEncounter codes"",
                        ""purpose"": ""To help implementers map from University of Michigan Epic to FHIR"",
                        ""group"": [{
                            ""source"": ""urn:oid:1.2.840.114350.1.72.1.7.7.10.696784.13260"",
                            ""target"": ""http://terminology.hl7.org/CodeSystem/v3-ActCode"",
                            ""element"": [
                                { ""code"": ""1"", ""target"": [{ ""code"": ""IMP"", ""display"": ""inpatient"", ""equivalence"": ""inexact"" }] },
                                { ""code"": ""2"", ""target"": [{ ""code"": ""IMP"", ""display"": ""inpatient"", ""equivalence"": ""inexact"" }] },
                                { ""code"": ""3"", ""target"": [{ ""code"": ""IMP"", ""display"": ""inpatient"", ""equivalence"": ""inexact"" }] },
                                { ""code"": ""4"", ""target"": [{ ""code"": ""IMP"", ""display"": ""inpatient"", ""equivalence"": ""inexact"" }] },
                                { ""code"": ""5"", ""target"": [{ ""code"": ""IMP"", ""display"": ""inpatient"", ""equivalence"": ""inexact"" }] },
                                { ""code"": ""6"", ""target"": [{ ""code"": ""IMP"", ""display"": ""inpatient"", ""equivalence"": ""inexact"" }] }
                            ]
                        }]
                    }"),
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
        var response = adminBffClient.Execute(request);
        Assert.IsTrue(response.StatusCode == System.Net.HttpStatusCode.OK, $"Response was not 200 OK {response.StatusCode}: {response.Content}");
    }

    private static async Task DeleteFacility()
    {
        Console.WriteLine("Deleting facility normalization...");
        var deleteNormalizationRequest = new RestRequest($"/normalization/{facilityId}", Method.Delete);
        var deleteNormalizationResponse = await adminBffClient.ExecuteAsync(deleteNormalizationRequest);

        if (deleteNormalizationResponse.StatusCode != HttpStatusCode.NoContent)
            Console.WriteLine($"Expected HTTP 204 No Content for normalization deletion but received {deleteNormalizationResponse.StatusCode}: {deleteNormalizationResponse.Content}");
        
        Console.WriteLine("Deleting facility...");
        var deleteFacilityRequest = new RestRequest($"/Facility/{facilityId}", Method.Delete);
        var deleteFacilityResponse = await adminBffClient.ExecuteAsync(deleteFacilityRequest);

        if (deleteFacilityResponse.StatusCode != HttpStatusCode.NoContent)
            Console.WriteLine($"Expected HTTP 204 No Content for facility deletion but received {deleteFacilityResponse.StatusCode}: {deleteFacilityResponse.Content}");
    }
}