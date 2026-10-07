using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Models;

public sealed class LogsHomePage
{
    public bool AcquisitionConfigured { get; init; }
    public bool AuditConfigured { get; init; }
    public string? KafkaUrl { get; init; }
    public string? GrafanaUrl { get; init; }
}

public sealed class AcquisitionQuery
{
    public string? FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? ReportId { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceType { get; set; }
    public string? QueryPhase { get; set; }
    public string? QueryType { get; set; }
    public List<string>? Status { get; set; }
    public string? Priority { get; set; }
    public bool IncludeDeleted { get; set; }
    public bool CancellableOnly { get; set; }
    public int MinAgeHours { get; set; } = LogsRules.DefaultMinAgeHours;
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = LogsRules.DefaultPageSize;

    public Dictionary<string, string> ToRoute(int? page = null, string? sortBy = null, string? sortDir = null)
    {
        var route = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(route, "facilityId", FacilityId);
        Add(route, "patientId", PatientId);
        Add(route, "reportId", ReportId);
        Add(route, "resourceId", ResourceId);
        Add(route, "resourceType", ResourceType);
        Add(route, "queryPhase", QueryPhase);
        Add(route, "queryType", QueryType);
        Add(route, "priority", Priority);
        if (Status is { Count: > 0 })
            route["status"] = string.Join(",", Status.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (IncludeDeleted)
            route["includeDeleted"] = "true";
        if (CancellableOnly)
            route["cancellableOnly"] = "true";
        if (MinAgeHours != LogsRules.DefaultMinAgeHours)
            route["minAgeHours"] = MinAgeHours.ToString();
        Add(route, "sortBy", sortBy ?? SortBy);
        Add(route, "sortDir", sortDir ?? SortDir);
        var size = LogsRules.ClampPageSize(PageSize);
        if (size != LogsRules.DefaultPageSize)
            route["pageSize"] = size.ToString();
        var number = page ?? (Page < 1 ? 1 : Page);
        if (number > 1)
            route["page"] = number.ToString();
        return route;
    }

    private static void Add(Dictionary<string, string> route, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            route[key] = value;
    }
}

public sealed class AcquisitionSearch
{
    public string? FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? ReportId { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceType { get; set; }
    public string? QueryPhase { get; set; }
    public string? QueryType { get; set; }
    public IReadOnlyList<string> Statuses { get; set; } = [];
    public string? Priority { get; set; }
    public bool IncludeDeleted { get; set; }
    public bool CancellableOnly { get; set; }
    public int MinAgeHours { get; set; } = LogsRules.DefaultMinAgeHours;
    public DateTime? CreatedBefore { get; set; }
    public string SortBy { get; set; } = "ExecutionDate";
    public string SortDir { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = LogsRules.DefaultPageSize;
    public string? Error { get; set; }
    public bool HasFilter { get; set; }
}

public sealed class AcquisitionLogListPage
{
    public AcquisitionQuery Query { get; set; } = new();
    public AcquisitionSearch Search { get; set; } = new();
    public string? LoadError { get; set; }
    public string? CountsNote { get; set; }
    public IReadOnlyList<StatusCountRow> Counts { get; set; } = [];
    public PageBar Paging { get; set; } = new();
    public IReadOnlyList<AcquisitionListRow> Logs { get; set; } = [];
}

public sealed class AcquisitionListRow
{
    public long Id { get; init; }
    public string FacilityId { get; init; } = string.Empty;
    public string PatientId { get; init; } = string.Empty;
    public string? ReportId { get; init; }
    public bool ReportLink { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Phase { get; init; } = string.Empty;
    public string QueryType { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Created { get; init; } = string.Empty;
    public DateTime? CreatedUtc { get; init; }
    public string Resources { get; init; } = string.Empty;
    public string ResourceId { get; init; } = string.Empty;
    public bool Deleted { get; init; }
    public bool CanProcess { get; init; }
}

public sealed class StatusCountRow
{
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class AcquisitionDetailPage
{
    public long Id { get; set; }
    public bool NotFound { get; set; }
    public string? LoadError { get; set; }
    public string? NotesError { get; set; }
    public string? ReferencesError { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string? ReportId { get; set; }
    public bool ReportLink { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
    public string? FhirVersion { get; set; }
    public int RetryAttempts { get; set; }
    public string Execution { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;
    public string Completed { get; set; } = string.Empty;
    public long? CompletionMilliseconds { get; set; }
    public bool ReferenceLog { get; set; }
    public int ReferenceCount { get; set; }
    public string Resources { get; set; } = string.Empty;
    public IReadOnlyList<string> AcquiredIds { get; set; } = [];
    public int AcquiredHidden { get; set; }
    public IReadOnlyList<string> Notes { get; set; } = [];
    public IReadOnlyList<FhirQueryRow> Queries { get; set; } = [];
    public IReadOnlyList<ReferenceResourceRow> References { get; set; } = [];
    public PageBar ReferencesPaging { get; set; } = new();
    public bool CanProcess { get; set; }
    public bool CanCancel { get; set; }
    public int MinAgeHours { get; set; } = LogsRules.DefaultMinAgeHours;
}

public sealed class FhirQueryRow
{
    public string Type { get; init; } = string.Empty;
    public string Resources { get; init; } = string.Empty;
    public string Parameters { get; init; } = string.Empty;
}

public sealed class ReferenceResourceRow
{
    public string ResourceType { get; init; } = string.Empty;
    public string ResourceId { get; init; } = string.Empty;
    public string Phase { get; init; } = string.Empty;
}

public sealed class SftpQuery
{
    public string? FacilityId { get; set; }
    public string? Status { get; set; }
    public string? AcquisitionType { get; set; }
    public string? SubType { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = LogsRules.DefaultPageSize;

    public Dictionary<string, string> ToRoute(int? page = null, string? sortBy = null, string? sortDir = null)
    {
        var route = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(route, "facilityId", FacilityId);
        Add(route, "status", Status);
        Add(route, "acquisitionType", AcquisitionType);
        Add(route, "subType", SubType);
        Add(route, "sortBy", sortBy ?? SortBy);
        Add(route, "sortDir", sortDir ?? SortDir);
        var size = LogsRules.ClampPageSize(PageSize);
        if (size != LogsRules.DefaultPageSize)
            route["pageSize"] = size.ToString();
        var number = page ?? (Page < 1 ? 1 : Page);
        if (number > 1)
            route["page"] = number.ToString();
        return route;
    }

    private static void Add(Dictionary<string, string> route, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            route[key] = value;
    }
}

public sealed class SftpSearch
{
    public string? FacilityId { get; set; }
    public string? Status { get; set; }
    public string? AcquisitionType { get; set; }
    public string? SubType { get; set; }
    public string SortBy { get; set; } = "ProcessDate";
    public string SortDir { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = LogsRules.DefaultPageSize;
    public string? Error { get; set; }
}

public sealed class SftpLogListPage
{
    public SftpQuery Query { get; set; } = new();
    public SftpSearch Search { get; set; } = new();
    public string? LoadError { get; set; }
    public PageBar Paging { get; set; } = new();
    public IReadOnlyList<SftpLogRow> Logs { get; set; } = [];
}

public sealed class SftpLogRow
{
    public Guid Id { get; init; }
    public string FacilityId { get; init; } = string.Empty;
    public string AcquisitionType { get; init; } = string.Empty;
    public string SubType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Scheduled { get; init; } = string.Empty;
    public string Processed { get; init; } = string.Empty;
    public int RetryAttempts { get; init; }
    public int FileCount { get; init; }
    public bool CanReset { get; init; }
}

public sealed class SftpDetailPage
{
    public Guid Id { get; set; }
    public bool NotFound { get; set; }
    public string? LoadError { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string AcquisitionType { get; set; } = string.Empty;
    public string SubType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Scheduled { get; set; } = string.Empty;
    public string Processed { get; set; } = string.Empty;
    public int RetryAttempts { get; set; }
    public string? TraceId { get; set; }
    public IReadOnlyList<string> Files { get; set; } = [];
    public IReadOnlyList<string> Notes { get; set; } = [];
    public IReadOnlyList<SftpBenchmarkRow> Benchmarks { get; set; } = [];
    public bool CanReset { get; set; }
}

public sealed class SftpBenchmarkRow
{
    public int Attempt { get; init; }
    public string Started { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
    public int Items { get; init; }
    public bool Succeeded { get; init; }
}

public sealed class AuditQuery
{
    public string? SearchText { get; set; }
    public string? FacilityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? Service { get; set; }

    // The name "action" is the MVC route value, so the filter uses eventAction.
    [FromQuery(Name = "eventAction")]
    public string? Action { get; set; }

    public string? User { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = LogsRules.DefaultPageSize;

    public Dictionary<string, string> ToRoute(int? page = null, string? sortBy = null, string? sortDir = null)
    {
        var route = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(route, "searchText", SearchText);
        Add(route, "facilityId", FacilityId);
        Add(route, "correlationId", CorrelationId);
        Add(route, "service", Service);
        Add(route, "eventAction", Action);
        Add(route, "user", User);
        Add(route, "sortBy", sortBy ?? SortBy);
        Add(route, "sortDir", sortDir ?? SortDir);
        var size = LogsRules.ClampAuditPageSize(PageSize);
        if (size != LogsRules.DefaultPageSize)
            route["pageSize"] = size.ToString();
        var number = page ?? (Page < 1 ? 1 : Page);
        if (number > 1)
            route["page"] = number.ToString();
        return route;
    }

    private static void Add(Dictionary<string, string> route, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            route[key] = value;
    }
}

public sealed class AuditSearch
{
    public string? SearchText { get; set; }
    public string? FacilityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? Service { get; set; }
    public string? Action { get; set; }
    public string? User { get; set; }
    public string SortBy { get; set; } = "CreatedOn";
    public string SortDir { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = LogsRules.DefaultPageSize;
    public string? Error { get; set; }
}

public sealed class AuditListPage
{
    public AuditQuery Query { get; set; } = new();
    public AuditSearch Search { get; set; } = new();
    public string? LoadError { get; set; }
    public PageBar Paging { get; set; } = new();
    public IReadOnlyList<AuditEventRow> Events { get; set; } = [];
}

public sealed class AuditEventRow
{
    public string Id { get; init; } = string.Empty;
    public bool Link { get; init; }
    public string FacilityId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string Service { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string User { get; init; } = string.Empty;
    public string When { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
}

public sealed class AuditDetailPage
{
    public string Id { get; set; } = string.Empty;
    public bool NotFound { get; set; }
    public string? LoadError { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string When { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public IReadOnlyList<AuditChangeRow> Changes { get; set; } = [];
}

public sealed class AuditChangeRow
{
    public string Name { get; init; } = string.Empty;
    public string Before { get; init; } = string.Empty;
    public string After { get; init; } = string.Empty;
}

public sealed class KafkaPage
{
    public string? Url { get; init; }
}

public sealed class LogsAction
{
    public LogsAction(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    public bool Succeeded { get; }
    public string Message { get; }
}
