using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;

namespace LantanaGroup.Link.DataAcquisition.Application.Models
{
    public class GetPatientDataRequest
    {
        public string FacilityId { get; set; }
        public ConsumeResult<string, DataAcquisitionRequested> ConsumeResult { get; set; }
        public string CorrelationId { get; set; }
        public QueryPlanType QueryPlanType { get; set; }
    }
}
