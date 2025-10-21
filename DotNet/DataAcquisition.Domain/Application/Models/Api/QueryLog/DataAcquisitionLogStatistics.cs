using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

public class DataAcquisitionLogStatistics
{
    public Dictionary<FhirQueryType, int> QueryTypeCounts { get; set; } = new();
    public Dictionary<QueryPhase, int> QueryPhaseCounts { get; set; } = new();
    public Dictionary<RequestStatus, int> RequestStatusCounts { get; set; } = new();
    public Dictionary<string, int> ResourceTypeCounts { get; set; } = new();
    public Dictionary<string, long> ResourceTypeCompletionTimeMilliseconds { get; set; } = new();
    public int TotalLogs { get; set; }
    public int TotalPatients { get; set; }
    public int TotalCompletedPatients { get; set; }
    public int TotalResourcesAcquired { get; set; }
    public int TotalRetryAttempts { get; set; }
    public ResourceCompletionTime FastestCompletionTimeMilliseconds { get; set; } = null!;
    public ResourceCompletionTime SlowestCompletionTimeMilliseconds { get; set; } = null!;
    public long TotalCompletionTimeMilliseconds { get; set; }
    public long AverageCompletionTimeMilliseconds => TotalLogs > 0 ? TotalCompletionTimeMilliseconds / TotalLogs : 0;
}

public record ResourceCompletionTime(string ResourceType, long CompletionTimeMilliseconds);