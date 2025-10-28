using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog
{
    public class UpdateDataAcquisitionLogModel
    {
        [DataMember]
        public long Id { get; set; }
        public DateTime? ScheduledExecutionDate { get; set; }
        public RequestStatusModel? Status { get; set; }
    }
}
