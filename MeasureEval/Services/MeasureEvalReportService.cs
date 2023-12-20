using Confluent.Kafka;
using Flurl;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Support;
using LantanaGroup.Link.MeasureEval.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Wrappers;
using System.Text;

namespace LantanaGroup.Link.MeasureEval.Services;

public class MeasureEvalReportService : IMeasureEvalReportService
{
    private readonly ILogger<MeasureEvalReportService> logger;
    private readonly HttpClient httpClient;
    private readonly MeasureEvalConfig measureEvalConfig;
    private readonly IKafkaWrapper<Ignore, Null, string, NotificationMessage> kafkaWrapper;

    public MeasureEvalReportService(ILogger<MeasureEvalReportService> logger, HttpClient httpClient, MeasureEvalConfig measureEvalConfig, IKafkaWrapper<Ignore, Null, string, NotificationMessage> kafkaWrapper)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(ILogger<MeasureEvalReportService>));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(HttpClient));
        this.measureEvalConfig = measureEvalConfig ?? throw new ArgumentNullException(nameof(MeasureEvalConfig));
        this.kafkaWrapper = kafkaWrapper ?? throw new ArgumentNullException(nameof(kafkaWrapper));
    }

    public MeasureReport? Evaluate(PatientDataNormalizedMessage message)
    {
        throw new NotImplementedException();
    }

    public async Task<MeasureReport?> EvaluateAsync(string Key, PatientDataNormalizedMessage message, string CorrelationId, CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(measureEvalConfig.EvaluationServiceUrl))
        {
            this.logger.LogError("CqfRulerEndpointConfigProperty is not configured. Cannot evaluate measure.");
            return null;
        }

        FhirJsonSerializer serializer = new FhirJsonSerializer();

        Parameters parameters = ConvertToParameters(message);
        HttpContent requestBody = new StringContent(serializer.SerializeToString(parameters), Encoding.UTF8, "application/json");
        string reportType = message.ScheduledReports.First<ScheduledReport>().ReportType;

        if (string.IsNullOrWhiteSpace(reportType))
        {
            this.logger.LogError("Report type is empty. Unable to call CQRF Ruler Endpoint.");
            return null;
        }

        string requestUrl = measureEvalConfig.EvaluationServiceUrl.AppendPathSegments("Measure", reportType, "$evaluate-measure");
        HttpResponseMessage response = null;
        string responseContent = null;
        string responseMessage = "";

        for (int x = 0; x < measureEvalConfig.MaxRetry; x++)
        {
            try
            {
                response = await httpClient.PostAsync(requestUrl, requestBody);
                responseContent = await response.Content.ReadAsStringAsync();
                break;
            }
            catch (Exception ex)
            {
                //Don't sleep on the last iteration since it won't do the request again if it fails anyway
                if (x != measureEvalConfig.MaxRetry - 1) Thread.Sleep(measureEvalConfig.RetryWait);
            }
        }

        if (response != null && response.IsSuccessStatusCode)
        {
            FhirJsonParser parser = new FhirJsonParser();
            MeasureReport measureReport = parser.Parse<MeasureReport>(responseContent);

            return measureReport;
        }
        else
        {
            if (response != null)
            {
                if (responseContent != null)
                {
                    var responseJson = System.Text.Json.JsonDocument.Parse(responseContent);

                    if (responseJson != null)
                    {
                        var issue = responseJson.RootElement.GetProperty("issue");

                        if (issue.GetArrayLength() > 0)
                        {
                            responseMessage = issue[0].GetProperty("diagnostics").ToString();
                        }
                    }
                }
                responseMessage = $"CQF-Ruler responded with non-success status code: {response.StatusCode} and message: {responseMessage}";
            }
            else
            {
                responseMessage = "CQF-Ruler couldn't be reached and no response was given";
            }

            logger.LogError($"Response Message: {responseMessage}");

            var headers = new Headers();
            string correlationId = (!string.IsNullOrEmpty(CorrelationId) ? new Guid(CorrelationId).ToString() : Guid.NewGuid().ToString());

            headers.Add("X-Correlation-Id", Encoding.ASCII.GetBytes(correlationId));

            await kafkaWrapper.ProduceKafkaMessageAsync(KafkaTopic.NotificationRequested.ToString(), () =>
                new Message<string, NotificationMessage>
                {
                    Key = Key,
                    Value = new NotificationMessage
                    {
                        NotificationType = "MeasureEvalFailed",
                        FacilityId = Key,
                        CorrelationId = correlationId,
                        Subject = response != null ? $"CQF-Ruler responded with non-success status code" : responseMessage,
                        Body = responseMessage
                    },
                    Headers = headers
                });

            return null;
        }
    }

    private Parameters ConvertToParameters(PatientDataNormalizedMessage message)
    {
        Parameters parameters = new Parameters();
        Bundle patientBundle = (Bundle)message.PatientBundle;
        parameters.Parameter.Add(new Parameters.ParameterComponent()
        {
            Name = "periodStart",
            Value = new FhirDateTime(message.ScheduledReports.First<ScheduledReport>().StartDate)
        });
        parameters.Parameter.Add(new Parameters.ParameterComponent()
        {
            Name = "periodEnd",
            Value = new FhirDateTime(message.ScheduledReports.First<ScheduledReport>().EndDate)
        });
        parameters.Parameter.Add(new Parameters.ParameterComponent()
        {
            Name = "subject",
            Value = new FhirString($"Patient/{message.PatientId.Replace("Patient/", "")}")
        }); 
        parameters.Parameter.Add(new Parameters.ParameterComponent()
        {
            Name = "additionalData",
            Resource = patientBundle
        });

        return parameters;
    }
}
