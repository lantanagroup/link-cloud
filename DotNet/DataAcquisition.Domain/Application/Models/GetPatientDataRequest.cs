using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models
{
    public class GetPatientDataRequest
    {
        public string FacilityId { get; set; } = string.Empty;
        public ConsumeResult<string, DataAcquisitionRequested> ConsumeResult { get; set; } = null!;
        public string CorrelationId { get; set; } = string.Empty;
        public QueryPlanType QueryPlanType { get; set; }
    }
}
