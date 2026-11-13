using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

/// <summary>
/// Returned by TryCompleteTailAsync when the inline tail check succeeds,
/// containing everything needed to produce the AcquisitionComplete Kafka message.
/// </summary>
public class TailCompletionResult
{
    public string FacilityId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public QueryPhase? QueryPhase { get; set; }
    public string? TraceParentId { get; set; }
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// When the group's first query log was created, so the time the whole acquisition took can be logged.
    /// Null when it couldn't be read.
    /// </summary>
    public DateTime? GroupStartedUtc { get; set; }
    public ResourcesAcquired ResourcesAcquired { get; set; } = new ResourcesAcquired();
}
