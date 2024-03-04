using Confluent.Kafka;
using Hl7.Fhir.Model;
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

public class MeasureEvalReportServiceTests
{
    private const string SuccessCqrfResponseFile = "CQRF/exampleCqrfRulerResponse.json";
    private const string FailureCOVIDMin500ResponseFile = "CQRF/COVIDMin500Response.json";
    private const string PatientDataNormalizedMessageSuccess = "Kafka/PatientDataNormalizedMessage.json";

    [Fact]
    public async Task EvaluateAsync_Success()
    {
        var cqrfResponse = LoadJson(SuccessCqrfResponseFile);
        var rawData = LoadJson(PatientDataNormalizedMessageSuccess);
        var data = LoadFhirResourceFromJson<Hl7.Fhir.Model.Bundle>(rawData);
        ScheduledReport scheduledReport = new ScheduledReport
        {
            ReportType = "NHSNGlycemicControlHypoglycemicInitialPopulation",
            StartDate = "2022-12-20",
            EndDate = "2022-12-21"
        };
        data.Id = "57f91b28-7fd4-bec8-aa69-c655e42a7906";
        var message = new PatientDataNormalizedMessage
        {
            PatientBundle = data,
            ScheduledReports = new List<ScheduledReport>()
        };
        message.ScheduledReports.Add(scheduledReport);

        var measureEvalConfig = new MeasureEvalConfig
        {
            EvaluationServiceUrl = "https://www.testcqfruler.com/fhir"
        };

        var evaluateOutput = await CreateReport(message, measureEvalConfig, cqrfResponse, HttpStatusCode.OK);

        Assert.True(evaluateOutput != null);
    }

    [Fact]
    public async Task EvaluateAsync_BadMessage()
    {
        var cqrfResponse = LoadJson(SuccessCqrfResponseFile);
        var rawData = LoadJson(PatientDataNormalizedMessageSuccess);
        var data = LoadFhirResourceFromJson<Hl7.Fhir.Model.Bundle>(rawData);
        //blank ReportType
        ScheduledReport scheduledReport = new ScheduledReport
        {
            ReportType = "",
            StartDate = "2022-12-20",
            EndDate = "2022-12-21"
        };
        data.Id = "57f91b28-7fd4-bec8-aa69-c655e42a7906";
        var message = new PatientDataNormalizedMessage
        {
            PatientBundle = data,
            ScheduledReports = new List<ScheduledReport>()
        };
        message.ScheduledReports.Add(scheduledReport);

        var measureEvalConfig = new MeasureEvalConfig
        {
            EvaluationServiceUrl = "https://www.testcqfruler.com/fhir"
        };

        var evaluateOutput = await CreateReport(message, measureEvalConfig, cqrfResponse, HttpStatusCode.OK);

        Assert.False(evaluateOutput != null);
    }

    [Fact]
    public async Task EvaluateAsync_FailCQRFRulerUnsuccessfulStatusCode()
    {
        var cqrfResponse = LoadJson(FailureCOVIDMin500ResponseFile);
        var rawData = LoadJson(PatientDataNormalizedMessageSuccess);
        var data = LoadFhirResourceFromJson<Hl7.Fhir.Model.Bundle>(rawData);
        ScheduledReport scheduledReport = new ScheduledReport
        {
            ReportType = "COVIDMin",
            StartDate = "2022-12-20",
            EndDate = "2022-12-21"
        };
        data.Id = "57f91b28-7fd4-bec8-aa69-c655e42a7906";
        var message = new PatientDataNormalizedMessage
        {
            PatientBundle = data,
            ScheduledReports = new List<ScheduledReport>()
        };
        message.ScheduledReports.Add(scheduledReport);

        var measureEvalConfig = new MeasureEvalConfig
        {
            EvaluationServiceUrl = "https://www.testcqfruler.com/fhir"
        };

        var evaluateOutput = await CreateReport(message, measureEvalConfig, cqrfResponse, HttpStatusCode.InternalServerError);

        Assert.False(evaluateOutput != null);
    }

    [Fact]
    public async Task EvaluateAsync_FailEmptyCQRFRulerEndpoint()
    {
        var cqrfResponse = LoadJson(SuccessCqrfResponseFile);
        var rawData = LoadJson(PatientDataNormalizedMessageSuccess);
        var data = LoadFhirResourceFromJson<Hl7.Fhir.Model.Bundle>(rawData);

        //blank ReportType
        ScheduledReport scheduledReport = new ScheduledReport
        {
            ReportType = "NHSNGlycemicControlHypoglycemicInitialPopulation",
            StartDate = "2022-12-20",
            EndDate = "2022-12-21"
        };
        data.Id = "57f91b28-7fd4-bec8-aa69-c655e42a7906";
        var message = new PatientDataNormalizedMessage
        {
            PatientBundle = data,
            ScheduledReports = new List<ScheduledReport>()
        };
        message.ScheduledReports.Add(scheduledReport);

        var measureEvalConfig = new MeasureEvalConfig
        {
            EvaluationServiceUrl = ""
        };

        var evaluateOutput = await CreateReport(message, measureEvalConfig, cqrfResponse, HttpStatusCode.OK);

        Assert.False(evaluateOutput != null);
    }

    private async Task<MeasureReport?> CreateReport(
        PatientDataNormalizedMessage message, 
        MeasureEvalConfig measureEvalConfig, 
        string cqrfResponse, 
        HttpStatusCode httpStatusCode)
    {
        //Mock HttpClient
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Post, $"{measureEvalConfig.EvaluationServiceUrl}/Measure/{message.ScheduledReports.First<ScheduledReport>().ReportType}/$evaluate-measure").Respond(httpStatusCode,"application/json", cqrfResponse);
        var httpClient = mockHttp.ToHttpClient();
 
        //Mock ILogger
        ILogger<MeasureEvalReportService> logger = Mock.Of<ILogger<MeasureEvalReportService>>();
        IKafkaWrapper<Confluent.Kafka.Ignore, Null, string, NotificationMessage> kafkaWrapper = Mock.Of<IKafkaWrapper<Ignore, Null, string, NotificationMessage>>();
        string facilityId = "testFacility";
        string CorrelationId = "testCorrelation";

        var reportService = new MeasureEvalReportService(logger, httpClient, measureEvalConfig, kafkaWrapper);
        var evaluateOutput = await reportService.EvaluateAsync(facilityId, message, CorrelationId);

        return evaluateOutput;
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
