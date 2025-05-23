namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

public class ReportSubmittedKey
{
    public string FacilityId { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}