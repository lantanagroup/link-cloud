namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

public class PayloadSubmittedKey
{
    public required  string FacilityId { get; set; }
    public required string ReportScheduleId { get; set; }
}