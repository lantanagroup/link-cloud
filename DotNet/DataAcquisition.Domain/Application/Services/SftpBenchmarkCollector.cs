using System.Diagnostics;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

/// <summary>
/// Collects performance benchmark data during SFTP file acquisition and processing.
/// Supports accumulating durations across multiple files when download and parse operations are interleaved.
/// </summary>
public class SftpBenchmarkCollector
{
    private readonly int _attemptNumber;
    private readonly DateTime _attemptStartedAt;
    private readonly long _startTimestamp;

    // Phase timestamps and accumulated durations
    private long _connectionAndRetrievalStartTimestamp;
    private double _connectionAndRetrievalDurationMs;
    private long _parseStartTimestamp;
    private double _parseDurationMs;

    // Counters
    private int _itemsProcessed;

    // Status
    private bool _isSuccessful;

    /// <summary>
    /// Creates a new benchmark collector for an SFTP acquisition attempt.
    /// </summary>
    /// <param name="attemptNumber">The attempt number (0-based).</param>
    public SftpBenchmarkCollector(int attemptNumber)
    {
        _attemptNumber = attemptNumber;
        _attemptStartedAt = DateTime.UtcNow;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Records the start of an SFTP connection and file retrieval operation.
    /// Can be called multiple times for interleaved operations - durations accumulate.
    /// </summary>
    public void StartConnectionAndRetrieval()
    {
        _connectionAndRetrievalStartTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Records the end of an SFTP connection and file retrieval operation.
    /// Duration is accumulated with previous calls for interleaved operations.
    /// </summary>
    public void EndConnectionAndRetrieval()
    {
        _connectionAndRetrievalDurationMs += Stopwatch.GetElapsedTime(_connectionAndRetrievalStartTimestamp).TotalMilliseconds;
    }

    /// <summary>
    /// Records the start of a file parsing operation.
    /// Can be called multiple times for interleaved operations - durations accumulate.
    /// </summary>
    public void StartParse()
    {
        _parseStartTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Records the end of a file parsing operation.
    /// Duration is accumulated with previous calls for interleaved operations.
    /// </summary>
    /// <param name="itemsProcessed">Number of items processed from the file. Accumulates across calls.</param>
    public void EndParse(int itemsProcessed)
    {
        _parseDurationMs += Stopwatch.GetElapsedTime(_parseStartTimestamp).TotalMilliseconds;
        _itemsProcessed += itemsProcessed;
    }

    /// <summary>
    /// Records whether the acquisition was successful.
    /// </summary>
    /// <param name="isSuccessful">True if the acquisition and processing completed successfully.</param>
    public void RecordSuccess(bool isSuccessful)
    {
        _isSuccessful = isSuccessful;
    }

    /// <summary>
    /// Builds the final benchmark model with all collected data.
    /// </summary>
    /// <returns>The benchmark model with all timing and count data.</returns>
    public SftpAcquisitionBenchmark Build()
    {
        var totalDurationMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;

        // Calculate overhead as the difference between total time and tracked phases
        var trackedPhasesMs = _connectionAndRetrievalDurationMs + _parseDurationMs;
        var overheadDurationMs = Math.Max(0, totalDurationMs - trackedPhasesMs);

        return new SftpAcquisitionBenchmark(
            AttemptNumber: _attemptNumber,
            AttemptStartedAt: _attemptStartedAt,
            ConnectionAndRetrievalDurationMs: _connectionAndRetrievalDurationMs,
            ParseDurationMs: _parseDurationMs,
            ItemsProcessed: _itemsProcessed,
            IsSuccessful: _isSuccessful,
            OverheadDurationMs: overheadDurationMs,
            TotalDurationMs: totalDurationMs
        );
    }
}
