namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

/// <summary>
/// Lightweight aggregate summary computed via DB-level queries.
/// Contains the same conceptual data as <see cref="DataAcquisitionLogStatistics"/>
/// but is cheaper to produce because no entities are materialised.
/// </summary>
public class DataAcquisitionReportSummary
{
    public string ReportId { get; set; } = string.Empty;
    public int TotalLogs { get; set; }
    public int TotalPatients { get; set; }
    public int TotalResourcesAcquired { get; set; }
    public int TotalRetryAttempts { get; set; }
    public long TotalCompletionTimeMs { get; set; }
    public long AverageCompletionTimeMs => TotalLogs > 0 ? TotalCompletionTimeMs / TotalLogs : 0;
    public List<StatusCount> StatusCounts { get; set; } = [];
    public List<ResourceTypeCount> ResourceTypeCounts { get; set; } = [];

    public class StatusCount
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ResourceTypeCount
    {
        public string ResourceType { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
