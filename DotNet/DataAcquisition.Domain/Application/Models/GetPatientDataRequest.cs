using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models
{
    public class GetPatientDataRequest
    {
        public string FacilityId { get; set; }
        public ConsumeResult<string, DataAcquisitionRequested> ConsumeResult { get; set; }
        public string CorrelationId { get; set; }
        public QueryPlanType QueryPlanType { get; set; }
    }
}
