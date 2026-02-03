namespace LantanaGroup.Link.DataAcquisition.Domain.Settings;

/// <summary>
/// Configuration settings for SFTP acquisition processing job.
/// </summary>
public class SftpAcquisitionSettings
{
    public const string SectionName = "SftpAcquisition";

    /// <summary>
    /// Enables parallel processing of SFTP acquisition logs.
    /// When false, logs are always processed sequentially regardless of backlog.
    /// </summary>
    public bool EnableParallelProcessing { get; set; } = true;

    /// <summary>
    /// Number of pending logs that triggers parallel processing (when enabled).
    /// If pending count is at or below this threshold, logs are processed sequentially.
    /// </summary>
    public int ParallelProcessingThreshold { get; set; } = 5;

    /// <summary>
    /// Maximum concurrent processing when parallel is active.
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Interval in seconds between job executions.
    /// </summary>
    public int JobIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retry attempts before marking as MaxRetriesReached.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
