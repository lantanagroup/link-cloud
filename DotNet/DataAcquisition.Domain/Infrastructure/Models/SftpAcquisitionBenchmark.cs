namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

/// <summary>
/// Benchmark data collected during SFTP file acquisition and processing.
/// </summary>
public record SftpAcquisitionBenchmark(
    int AttemptNumber,
    DateTime AttemptStartedAt,
    double ConnectionAndRetrievalDurationMs,
    double ParseDurationMs,
    int ItemsProcessed,
    bool IsSuccessful,
    double OverheadDurationMs,
    double TotalDurationMs
);
