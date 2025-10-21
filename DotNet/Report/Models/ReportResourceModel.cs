namespace LantanaGroup.Link.Report.Models;

public class ReportResourceModel
{
    public Guid Id { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public Guid ReportScheduleId { get; set; }
    public string PatientId { get; set; } = string.Empty;
    public string MeasureReportId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
}