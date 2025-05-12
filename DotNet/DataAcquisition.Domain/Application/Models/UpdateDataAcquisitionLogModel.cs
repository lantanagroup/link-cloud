namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models
{
    public class UpdateDataAcquisitionLogModel
    {
        public DateTime? ScheduledExecutionDate { get; set; }
        public RequestStatusModel? Status { get; set; }
    }
}
