namespace LantanaGroup.Link.Shared.Application.Interfaces;

/// <summary>
/// Cross-service flag that a facility/report pipeline has been aborted so Kafka
/// listeners can ack leftover messages without doing more work. Error topics are
/// not consulted; those stay for debug.
/// </summary>
public interface IPipelineAbortRegistry
{
    Task AbortAsync(string? facilityId, string? reportId, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    Task<bool> IsAbortedAsync(string? facilityId, string? reportId, CancellationToken cancellationToken = default);

    Task ClearAsync(string? facilityId, string? reportId, CancellationToken cancellationToken = default);
}
