using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

public class DataAcquisitionLogApiModel
{
    public long Id { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string? PatientId { get; set; }
    public string? ReportTrackingId { get; set; }
    public RequestStatus? Status { get; set; }
    public QueryPhase? QueryPhase { get; set; }
    public List<FhirQueryApiModel> FhirQuery { get; set; } = [];
    public List<string>? ResourceAcquiredIds { get; set; } = [];
    public List<ReferenceResourceApiModel> ReferenceResources { get; set; } = [];
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

public class CreateFhirQueryConfigurationRequestApiModel
{
    public string FacilityId { get; set; } = string.Empty;
    public string FhirServerBaseUrl { get; set; } = string.Empty;
    public int MaxConcurrentRequests { get; set; }
    public int? MaxRetries { get; set; }
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
    Pending,
    [StringValue("Ready")]
    Ready,
    [StringValue("Queued")]
    Queued,
    [StringValue("Processing")]
    Processing,
    [StringValue("Completed")]
    Completed,
    [StringValue("Failed")]
    Failed,
    [StringValue("Max Retries Reached")]
    MaxRetriesReached,
    [StringValue("Skipped")]
    Skipped,
    [StringValue("Configuration Required")]
    ConfigurationRequired
}

public enum QueryPhase
{
    [StringValue("Initial")]
    Initial,
    [StringValue("Supplemental")]
    Supplemental,
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
            "referential" => QueryPhase.Referential,
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
}
