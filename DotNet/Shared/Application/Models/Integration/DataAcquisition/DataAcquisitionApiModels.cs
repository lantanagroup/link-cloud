using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

public class DataAcquisitionLogApiModel
{
    public long Id { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string? PatientId { get; set; }
    public string? CorrelationId { get; set; }
    public string? ReportTrackingId { get; set; }
    public string? TraceId { get; set; }
    public string? FhirVersion { get; set; }
    public string? Priority { get; set; }
    public int? RetryAttempts { get; set; }
    public RequestStatus? Status { get; set; }
    public QueryPhase? QueryPhase { get; set; }
    public DateTime? CompletionDate { get; set; }
    public long? CompletionTimeMilliseconds { get; set; }
    public bool IsReferenceLog { get; set; }
    public List<string> ResourceTypes { get; set; } = [];
    public List<FhirQueryApiModel> FhirQuery { get; set; } = [];
    public List<string>? ResourceAcquiredIds { get; set; } = [];
    public int ReferenceResourceCount { get; set; }
    public List<string>? Notes { get; set; } = [];
}

public class ReferenceResourceApiModel
{
    public Guid Id { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ReferenceResource { get; set; }
    public QueryPhase? QueryPhase { get; set; }
    public long DataAcquisitionLogId { get; set; }
}

public class FhirQueryApiModel
{
    public Guid? Id { get; set; }
    public string? FacilityId { get; set; }
    public FhirQueryType QueryType { get; set; }
    public IEnumerable<string> ResourceTypes { get; set; } = [];
    public List<string> QueryParameters { get; set; } = [];
}

public enum FhirQueryType
{
    [StringValue("Read")]
    Read,
    [StringValue("Search")]
    Search,
    [StringValue("SearchPost")]
    SearchPost,
    [StringValue("BulkDataRequest")]
    BulkDataRequest,
    [StringValue("BulkDataPoll")]
    BulkDataPoll
}

public static class FhirQueryTypeUtilities
{
    public static FhirQueryType ToDomain(string fhirQueryType)
    {
        return fhirQueryType switch
        {
            "Read" => FhirQueryType.Read,
            "Search" => FhirQueryType.Search,
            "SearchPost" => FhirQueryType.SearchPost,
            "BulkDataRequest" => FhirQueryType.BulkDataRequest,
            "BulkDataPoll" => FhirQueryType.BulkDataPoll,
            _ => throw new ArgumentOutOfRangeException(nameof(fhirQueryType), fhirQueryType, null)
        };
    }
}

public class DataAcquisitionBulkActionResultApiModel
{
    public int Requested { get; set; }
    public int Cancelled { get; set; }
    public int Ineligible { get; set; }
}

public class CreateFhirQueryConfigurationRequestApiModel
{
    public string FacilityId { get; set; } = string.Empty;
    public string FhirServerBaseUrl { get; set; } = string.Empty;
    public int MaxConcurrentRequests { get; set; }
    public int? MaxRetries { get; set; }
    public TimeSpan? MinAcquisitionPullTime { get; set; }
    public TimeSpan? MaxAcquisitionPullTime { get; set; }
    public string? TimeZone { get; set; }
}

public class CreateQueryPlanRequestApiModel
{
    public string? PlanName { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string EHRDescription { get; set; } = string.Empty;
    public string LookBack { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object> InitialQueries { get; set; } = [];
    public Dictionary<string, object> SupplementalQueries { get; set; } = [];
}

public class DataAcquisitionLogStatusStatisticsApiModel
{
    public string ReportId { get; set; } = string.Empty;
    public string? PatientId { get; set; }
    public List<DataAcquisitionLogStatusCountApiModel> Statuses { get; set; } = [];
}

public class DataAcquisitionLogStatusCountApiModel
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public enum RequestStatus
{
    [StringValue("Pending")]
    [CancellableStatus]
    Pending,
    [StringValue("Ready")]
    [CancellableStatus]
    Ready,
    [StringValue("Queued")]
    [CancellableStatus]
    Queued,
    [StringValue("Processing")]
    [CancellableStatus]
    Processing,
    [StringValue("Completed")]
    [TerminalStatus]
    Completed,
    [StringValue("Failed")]
    [CancellableStatus]
    Failed,
    [StringValue("Max Retries Reached")]
    [TerminalStatus]
    MaxRetriesReached,
    [StringValue("Skipped")]
    [TerminalStatus]
    Skipped,
    [StringValue("Configuration Required")]
    [CancellableStatus]
    ConfigurationRequired,
    [StringValue("Cancelled")]
    [TerminalStatus]
    Cancelled,
    [StringValue("Configuration Missing")]
    [TerminalStatus]
    ConfigurationMissing,

    [StringValue("Not Reportable")]
    [TerminalStatus]
    NotReportable
}

public enum QueryPhase
{
    [StringValue("Initial")]
    Initial,
    [StringValue("Supplemental")]
    Supplemental,
    [Obsolete("Referential phase is no longer emitted on the primary log or downstream contracts; Use Initial or Supplemental instead. Retaining for backward compatibility.")]
    [StringValue("Referential")]
    Referential,
    [StringValue("Polling")]
    Polling,
    [StringValue("Monitoring")]
    Monitoring
}

public static class QueryPhaseExtensions
{
    public static QueryPhase TranslateToQueryPhase(this string queryPhaseStr)
    {
        return queryPhaseStr.ToLower() switch
        {
            "initial" => QueryPhase.Initial,
            "supplemental" => QueryPhase.Supplemental,
            "polling" => QueryPhase.Polling,
            "monitoring" => QueryPhase.Monitoring,
            _ => throw new ArgumentException($"Invalid value: {queryPhaseStr}")
        };
    }
}

public static class QueryPhaseUtilities
{
    public static QueryPhase ToDomain(string queryPlanType)
    {
        return queryPlanType.ToLower() switch
        {
            "initial" => QueryPhase.Initial,
            "supplemental" => QueryPhase.Supplemental,
            _ => throw new ArgumentOutOfRangeException(nameof(queryPlanType), queryPlanType, null)
        };
    }

    /// <summary>
    /// Translates a <see cref="QueryPhase"/> into the string value safe to put on the
    /// downstream <c>ResourceAcquired</c> / <c>ResourceNormalized</c> wire contract.
    /// The Java <c>measureeval</c> service's <c>QueryType</c> enum only declares
    /// <c>INITIAL</c> and <c>SUPPLEMENTAL</c>; emitting any other value causes a Jackson
    /// deserialization failure that dead-letters the message. Initial / Supplemental pass
    /// through; null becomes empty. Other phases (Polling, Monitoring) are not currently
    /// produced on this contract and fall through to their raw name so a real bug surfaces
    /// loudly rather than being silently rewritten.
    /// </summary>
    public static string ToWireQueryType(QueryPhase? queryPhase) => queryPhase switch
    {
        null => string.Empty,
        _ => queryPhase.Value.ToString(),
    };
}
