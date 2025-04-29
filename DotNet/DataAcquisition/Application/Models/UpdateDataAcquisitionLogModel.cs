namespace LantanaGroup.Link.DataAcquisition.Application.Models
{
    public class UpdateDataAcquisitionLogModel
    {
        public DateTime? ScheduledExecutionDate { get; set; }
        public RequestStatusModel? Status { get; set; }
    }
}
