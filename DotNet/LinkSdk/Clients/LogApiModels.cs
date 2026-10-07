using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Sdk.Clients;

/// <summary>
/// Filters for <c>GET /api/data/acquisition-logs</c>. Blank values are omitted from the query string.
/// </summary>
public sealed class AcquisitionLogQuery
{
    public string? FacilityId { get; init; }
    public string? ReportId { get; init; }
    public string? PatientId { get; init; }
    public string? ResourceId { get; init; }
    public string? ResourceType { get; init; }
    public string? QueryPhase { get; init; }
    public string? QueryType { get; init; }
    public IReadOnlyList<string>? Statuses { get; init; }
    public string? Priority { get; init; }
    public bool IncludeDeleted { get; init; }
    public DateTime? CreatedBefore { get; init; }
    public string? SearchTerm { get; init; }
    public int PageSize { get; init; } = 10;
    public int PageNumber { get; init; } = 1;
    public string SortBy { get; init; } = "ExecutionDate";
    public string SortOrder { get; init; } = "Descending";
}

/// <summary>
/// One row of <c>GET /api/data/acquisition-logs</c>. The search payload is a summary, which carries
/// resource id and query type that the detail model does not.
/// </summary>
public sealed class DataAcquisitionLogSummaryApiModel
{
    public long Id { get; set; }
    public string? FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? ReportTrackingId { get; set; }
    public string? Priority { get; set; }
    public RequestStatus? Status { get; set; }
    public QueryPhase? QueryPhase { get; set; }
    public FhirQueryType? QueryType { get; set; }
    public DateTime? ExecutionDate { get; set; }
    public DateTime? CreateDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public int? RetryAttempts { get; set; }
    public bool IsDeleted { get; set; }
    public List<string>? ResourceTypes { get; set; }
    public string? ResourceId { get; set; }
}

public sealed class SftpLogApiModel
{
    public Guid? ExternalId { get; set; }
    public string? FacilityId { get; set; }
    public string? AcquisitionType { get; set; }
    public string? SubType { get; set; }
    public List<string>? FileNames { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ProcessDate { get; set; }
    public int? RetryAttempts { get; set; }
    public string? Status { get; set; }
    public string? OriginatingTraceId { get; set; }
    public List<string>? Notes { get; set; }
    public List<SftpLogBenchmarkApiModel>? Benchmarks { get; set; }
}

public sealed class SftpLogBenchmarkApiModel
{
    public int AttemptNumber { get; set; }
    public DateTime AttemptStartedAt { get; set; }
    public double TotalDurationMs { get; set; }
    public int ItemsProcessed { get; set; }
    public bool IsSuccessful { get; set; }
}

public sealed class AuditEventApiModel
{
    public string? Id { get; set; }
    public string? FacilityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? ServiceName { get; set; }
    public DateTime? EventDate { get; set; }
    public string? User { get; set; }
    public string? Action { get; set; }
    public string? Resource { get; set; }
    public string? Notes { get; set; }
    public List<PropertyChangeModel>? PropertyChanges { get; set; }
}

public sealed class PagedAuditApiModel
{
    public List<AuditEventApiModel> Records { get; set; } = [];
    public LantanaGroup.Link.Shared.Application.Models.Responses.PaginationMetadata? Metadata { get; set; }
}
