using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Submission.KafkaProducers;

public class ReportSubmittedProducer(IProducer<ReportSubmittedKey, ReportSubmittedValue> producer)
{
    public void Produce(string? correlationId, string facilityId, DateTime startDate, DateTime endDate, string reportTrackingId)
    {
        if (correlationId == null)
            correlationId = Guid.NewGuid().ToString();
        
        producer.Produce(nameof(KafkaTopic.ReportSubmitted), new Message<ReportSubmittedKey, ReportSubmittedValue>
        {
            Key = new ReportSubmittedKey()
            {
                FacilityId = facilityId,
                StartDate = startDate,
                EndDate = endDate
            },
            Value = new ReportSubmittedValue()
            {
                ReportTrackingId = reportTrackingId
            },
            Headers = new Headers()
            {
                { "X-Correlation-Id", Encoding.UTF8.GetBytes(correlationId) }
            }
        });
        
        producer.Flush();
    }
}