using LantanaGroup.Link.Shared.Application.Models.Census;

namespace LantanaGroup.Link.Shared.Application.Models.Report;

public class ScheduledReportListSummary
{
    public required string Id { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public DateTime ReportStartDate { get; set; }
    public DateTime ReportEndDate { get; set; }
    public bool Submitted { get; set; }
    public DateTime? SubmitDate { get; set; }
    public List<string> ReportTypes { get; set; } = [];
    public Frequency Frequency { get; set; }
    public int CensusCount { get; set; }
    public int InitialPopulationCount { get; set; }
    public ScheduledReportMetrics? ReportMetrics { get; set; }
    public string? PayloadRootUri { get; set; }
}

public class ScheduledReportMetrics
{
    public Dictionary<string, int> MeasureIpCounts { get; set; } = new();
    public Dictionary<string, int> ReportStatusCounts { get; set; } = new();
    public Dictionary<string, int> ValidationStatusCounts { get; set; } = new();
}