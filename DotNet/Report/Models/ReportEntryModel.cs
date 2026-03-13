using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Report.Models;
public class ReportEntryModel
{
    public Guid Id { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public Guid ReportScheduleId { get; set; }
    public string PatientId { get; set; } = string.Empty;
    public ReportingStatus ReportingStatus { get; set; }
    public SubmissionStatus? SubmissionStatus { get; set; }
    public DateTime? SubmitReportDateTime { get; set; }
    public string AggregateReportUri { get; set; } = string.Empty;
    public string AggregateReportBlobName { get; set; } = string.Empty;

    public List<EntryMeasureReportModel> MeasureReports { get; set; } = new();
}

public class EntryMeasureReportModel
{
    public string MeasureReportId { get; set; } = string.Empty;
    public MeasureReportStatus Status { get; set; } = MeasureReportStatus.EntryCreated;
    public string ReportType { get; set; } = string.Empty;
    public string? MeasureReportUri { get; set; }
    public string? MeasureReportFileName { get; set; }
    public Dictionary<string, int> ResourceCount { get; set; } = new();
}