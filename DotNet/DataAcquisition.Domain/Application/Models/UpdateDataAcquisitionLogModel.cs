using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models
{
    public class UpdateDataAcquisitionLogModel
    {
        [Required, DataMember]
        public required long Id { get; set; }
        public DateTime? ScheduledExecutionDate { get; set; }
        public RequestStatusModel? Status { get; set; }
    }
}
