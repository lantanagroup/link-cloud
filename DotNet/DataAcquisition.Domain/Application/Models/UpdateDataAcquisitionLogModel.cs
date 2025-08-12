namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models
{
    public class UpdateDataAcquisitionLogModel
    {
        public string? Id { get; set; }
        public DateTime? ScheduledExecutionDate { get; set; }
        public RequestStatusModel? Status { get; set; }
    }
}
