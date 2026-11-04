namespace LantanaGroup.Link.Report.Domain.Enums;

/// <summary>
/// Where a report stands in data retention. Stored on the report and never sent on Kafka.
/// </summary>
public enum ReportRetentionStatus
{
    /// <summary>
    /// The report has never been part of a retention cleanup, so a scheduled run may select it.
    /// </summary>
    None,

    /// <summary>
    /// The report is part of a cleanup that is in progress, partial or failed. Its clinical data can no
    /// longer be downloaded.
    /// </summary>
    PendingDeletion,

    /// <summary>
    /// Every expected report-scoped participant has deleted the report's clinical data.
    /// </summary>
    Scrubbed,
}
