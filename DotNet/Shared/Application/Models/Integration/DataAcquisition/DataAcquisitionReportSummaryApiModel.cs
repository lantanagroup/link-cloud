namespace LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

/// <summary>
/// Lightweight summary of data acquisition activity for a report.
/// All fields are computed via DB aggregates — no entity materialisation.
/// Intended for dashboard polling and monitoring.
/// </summary>
public class DataAcquisitionReportSummaryApiModel
{
    public string ReportId { get; set; } = string.Empty;
    public int TotalLogs { get; set; }
    public int TotalPatients { get; set; }
    public int TotalResourcesAcquired { get; set; }
    public int TotalRetryAttempts { get; set; }
    public long TotalCompletionTimeMs { get; set; }
    public long AverageCompletionTimeMs => TotalLogs > 0 ? TotalCompletionTimeMs / TotalLogs : 0;
    public List<StatusCountEntry> StatusCounts { get; set; } = [];
    public List<ResourceTypeCountEntry> ResourceTypeCounts { get; set; } = [];

    public class StatusCountEntry
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ResourceTypeCountEntry
    {
        public string ResourceType { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
