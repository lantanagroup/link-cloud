namespace LantanaGroup.Link.Shared.Application.Models.Kafka
{
    public class SubmitPayloadKey
    {
        public string FacilityId { get; set; } = string.Empty;
        public required Guid ReportScheduleId { get; set; }
    }
}
