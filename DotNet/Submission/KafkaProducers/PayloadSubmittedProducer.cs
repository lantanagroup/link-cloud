using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Submission.KafkaProducers;

public class PayloadSubmittedProducer(IProducer<string, PayloadSubmittedValue> producer)
{
    public void Produce(string? correlationId, string facilityId, Guid reportScheduleId, PayloadType payloadType, string? patientId = null)
    {
        if (correlationId == null)
            correlationId = Guid.NewGuid().ToString();

        var key = KafkaKeys.ForReport(facilityId, reportScheduleId);

        try
        {
            producer.Produce(nameof(KafkaTopic.PayloadSubmitted), new Message<string, PayloadSubmittedValue>
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
            });

            producer.Flush();
        }
        catch (ProduceException<string, PayloadSubmittedValue> ex)
        {
            throw new Exception($"Failed to produce PayloadSubmitted message for facility: {facilityId}: {ex.Message}");
        }
    }
}