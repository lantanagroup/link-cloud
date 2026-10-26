using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Submission.KafkaProducers;

public class PayloadSubmittedProducer(IProducer<string, PayloadSubmittedValue> producer)
{
    public async Task ProduceAsync(
        string? correlationId,
        string facilityId,
        Guid reportScheduleId,
        PayloadType payloadType,
        string? patientId,
        CancellationToken cancellationToken)
    {
        if (correlationId == null)
            correlationId = Guid.NewGuid().ToString();

        var key = KafkaKeys.ForReport(facilityId, reportScheduleId);

        // ProduceAsync throws if the broker rejects the record or the delivery times out.
        // The caller leaves the source offset uncommitted so the completion is retried.
        await producer.ProduceAsync(nameof(KafkaTopic.PayloadSubmitted), new Message<string, PayloadSubmittedValue>
        {
            Key = key,
            Value = new PayloadSubmittedValue()
            {
                PayloadType = payloadType,
                FacilityId = facilityId,
                ReportScheduleId = reportScheduleId,
                PatientId = patientId
            },
            Headers = new Headers()
            {
                { "X-Correlation-Id", Encoding.UTF8.GetBytes(correlationId) }
            }
        }, cancellationToken);
    }
}