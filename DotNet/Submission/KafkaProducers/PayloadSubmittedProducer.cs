using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Submission.KafkaProducers;

public class PayloadSubmittedProducer(IProducer<PayloadSubmittedKey, PayloadSubmittedValue> producer)
{
    public void Produce(string? correlationId, string facilityId, string reportScheduleId, PayloadType payloadType, string? patientId = null)
    {
        if (correlationId == null)
            correlationId = Guid.NewGuid().ToString();

        try
        {
            producer.Produce(nameof(KafkaTopic.PayloadSubmitted), new Message<PayloadSubmittedKey, PayloadSubmittedValue>
            {
                Key = new PayloadSubmittedKey()
                {
                    FacilityId = facilityId,
                    ReportScheduleId = reportScheduleId,

                },
                Value = new PayloadSubmittedValue()
                {
                    PayloadType = payloadType,
                    PatientId = patientId
                },
                Headers = new Headers()
                {
                    { "X-Correlation-Id", Encoding.UTF8.GetBytes(correlationId) }
                }
            });

            producer.Flush();
        }
        catch (ProduceException<PayloadSubmittedKey, PayloadSubmittedValue> ex)
        {
            throw new Exception($"Failed to produce PayloadSubmitted message for facility: {facilityId}: {ex.Message}");
        }
    }
}