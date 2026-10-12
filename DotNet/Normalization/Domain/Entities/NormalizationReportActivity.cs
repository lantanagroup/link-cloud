namespace LantanaGroup.Link.Normalization.Domain.Entities;

/// <summary>
/// One row per report, acquisition pass (correlation id) and query type. A redelivered
/// message rewrites its own row instead of adding to a total, so counts stay exact.
/// The key leads with the report id, so a report's summary is one range seek.
/// </summary>
public class NormalizationReportActivity
{
    public string ReportTrackingId { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string QueryType { get; set; } = "";
    public string FacilityId { get; set; } = "";
    public int Resources { get; set; }
    public int Failures { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}