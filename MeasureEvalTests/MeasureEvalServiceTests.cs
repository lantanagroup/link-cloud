using Confluent.Kafka;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.MeasureEval.Models;
using LantanaGroup.Link.MeasureEval.Services;
using LantanaGroup.Link.Shared.Application.Wrappers;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using System.Net;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace MeasureEvalUnitTests;

public class MeasureEvalServiceTests
{
    private const string ExampleMeasureBundle = "CQRF/NHSNGlycemicControlHypoglycemicInitialPopulation-bundle.json";
    private const string Hypo500Response = "CQRF/NHSNGlycemicControlHypoglycemicInitialPopulation500Response.json";
    private const string Hypo200Response = "CQRF/HypoCQFRulerResponse.json";
    private const string HypoDeleteResponse = "CQRF/HypoDeleteResponse.json";

    //Mock ILogger
    public ILogger<MeasureEvalService> logger = Mock.Of<ILogger<MeasureEvalService>>();

    [Fact]
    public async Task UpdateMeasure()
    {
        var cqfrResponse = LoadJson(Hypo200Response);
        var rawData = LoadJson(ExampleMeasureBundle);

        var measureId = "NHSNGlycemicControlHypoglycemicInitialPopulation";
        
        var measureEvalConfig = new MeasureEvalConfig
        {
            EvaluationServiceUrl = "https://www.testcqfruler.com/fhir",
            DataStoreServiceUrl = "https://ehr-test.com/fhir"
        };

        var httpClient = CreateHttpClient(HttpMethod.Post, measureEvalConfig.EvaluationServiceUrl, HttpStatusCode.OK, cqfrResponse);
        var measureEvalService = new MeasureEvalService(logger, httpClient, Mock.Of<IKafkaWrapper<Ignore, Null, Null, MeasureChanged>>(), measureEvalConfig);

        var response = await measureEvalService.UpdateMeasure(measureId, rawData);

        Assert.True(response != null);
    }

    private HttpClient CreateHttpClient(
        HttpMethod httpMethod,
        String endpoint,
        HttpStatusCode httpStatusCode,
        String response)
    {
        //Mock HttpClient
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(httpMethod, $"{endpoint}").Respond(httpStatusCode, "application/json", response);
        var httpClient = mockHttp.ToHttpClient();

        return httpClient;
    }

    private HttpClient CreateGetAndUpdateHttpClient(
        HttpMethod cqfRulerHttpMethod,
        HttpMethod datastoreHttpMethod,
        String cqfRulerEndpoint,
        String datastoreEndpoint,
        HttpStatusCode cqfRulerStatusCode,
        HttpStatusCode datastoreStatusCode,
        String cqfRulerResponse,
        String datastoreResponse)
    {
        //Mock HttpClient
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(cqfRulerHttpMethod, $"{cqfRulerEndpoint}").Respond(cqfRulerStatusCode, "application/json", cqfRulerResponse);
        mockHttp.When(datastoreHttpMethod, $"{datastoreEndpoint}").Respond(datastoreStatusCode, "application/json", datastoreResponse);
        var httpClient = mockHttp.ToHttpClient();

        return httpClient;
    }

    private string LoadJson(string fileName)
    {

        //using StreamReader r = new StreamReader($"{AppContext.BaseDirectory}/{fileName}");
        var file = System.IO.File.ReadAllText($"{AppContext.BaseDirectory}/Resources/{fileName}");
        return file;
    }

    private T LoadFhirResourceFromJson<T>(string json)
    {
        var options = new JsonSerializerOptions().ForFhir(typeof(T).Assembly);
        var data = System.Text.Json.JsonSerializer.Deserialize<T>(json, options);
        return data;
    }
}
