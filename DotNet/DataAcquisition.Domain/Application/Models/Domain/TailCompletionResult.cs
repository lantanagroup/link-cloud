using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

/// <summary>
/// Returned by TryCompleteTailAsync when the inline tail check succeeds,
/// containing the phase outcome and the data for the successful ResourcesAcquired handoff.
/// </summary>
public class TailCompletionResult
{
    public string FacilityId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public QueryPhase? QueryPhase { get; set; }
    public string? TraceParentId { get; set; }
    public string PatientId { get; set; } = string.Empty;
    public List<long> FailedLogIds { get; set; } = [];
    public bool IsSuccess => FailedLogIds.Count == 0;
    public long? ProcessingTime { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public ResourcesAcquired ResourcesAcquired { get; set; } = new ResourcesAcquired();
}
