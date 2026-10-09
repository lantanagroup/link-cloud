using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Report.KafkaProducers;

/// <summary>
/// Tells Report that one patient finished validation when submission is off.
/// The key is the report, so PayloadSubmittedListener serializes the manifest check.
/// </summary>
public class PayloadSubmittedSignalProducer(IProducer<string, PayloadSubmittedValue> producer)
{
    public async Task ProduceAsync(
        string? correlationId,
        string facilityId,
        Guid reportScheduleId,
        string? patientId,
        string? metricsMode,
        CancellationToken cancellationToken)
    {
        var headers = new Headers
        {
            { "X-Correlation-Id", Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString() : correlationId) }
        };
        if (!string.IsNullOrWhiteSpace(metricsMode))
        {
            KafkaHeaderHelper.SetMetricsMode(headers, metricsMode);
        }

        await producer.ProduceAsync(
            nameof(KafkaTopic.PayloadSubmitted),
            new Message<string, PayloadSubmittedValue>
            {
                Key = KafkaKeys.ForReport(facilityId, reportScheduleId),
                Value = new PayloadSubmittedValue
                {
                    PayloadType = PayloadType.MeasureReportSubmissionEntry,
                    FacilityId = facilityId,
                    ReportScheduleId = reportScheduleId,
                    PatientId = patientId
                },
                Headers = headers
            },
            cancellationToken);
    }
}
