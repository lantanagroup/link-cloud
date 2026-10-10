using Link.UI.Services;

namespace Link.UI.Models;

public sealed class ReportsListQuery
{
    public string? FacilityId { get; set; }
    public string? ReportId { get; set; }
    public List<string>? Status { get; set; }
    public string? Frequency { get; set; }
    public string? Created { get; set; }
    public string? PeriodFrom { get; set; }
    public string? PeriodTo { get; set; }
    public bool ShowDeleted { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public string? Scope { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = FacilityViewRules.DefaultPageSize;

    public ReportsListQuery WithFacility(string facilityId, int page, int pageSize) => new()
    {
        FacilityId = facilityId,
        ReportId = ReportId,
        Status = Status,
        Frequency = Frequency,
        Created = Created,
        PeriodFrom = PeriodFrom,
        PeriodTo = PeriodTo,
        ShowDeleted = ShowDeleted,
        SortBy = SortBy,
        SortDir = SortDir,
        Scope = Scope,
        Page = page,
        PageSize = pageSize
    };

    public string AutomationFingerprint() => string.Join('\u001f', new[]
    {
        ReportId,
        Frequency,
        Created,
        PeriodFrom,
        PeriodTo,
        ShowDeleted ? "1" : "0",
        Status is null ? "" : string.Join(',', Status),
        SortBy,
        SortDir
    });

    public Dictionary<string, string> ToRoute(int? page = null, string? sortBy = null, string? sortDir = null, int? pageSize = null)
    {
        var route = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(route, "facilityId", FacilityId);
        Add(route, "reportId", ReportId);
        if (Status is { Count: > 0 })
            route["status"] = string.Join(",", Status.Where(value => !string.IsNullOrWhiteSpace(value)));
        Add(route, "frequency", Frequency);
        Add(route, "created", Created);
        Add(route, "periodFrom", PeriodFrom);
        Add(route, "periodTo", PeriodTo);
        if (ShowDeleted)
            route["showDeleted"] = "true";
        AutomationMarkRules.AddScope(route, Scope);
        Add(route, "sortBy", sortBy ?? SortBy);
        Add(route, "sortDir", sortDir ?? SortDir);

        var chosen = pageSize ?? PageSize;
        var size = FacilityViewRules.PageSizes.Contains(chosen) ? chosen : FacilityViewRules.DefaultPageSize;
        if (size != FacilityViewRules.DefaultPageSize)
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

public sealed class ReportsListModel
{
    public ReportsListQuery Query { get; set; } = new();
    public string? LoadError { get; set; }
    public PageBar Paging { get; set; } = new();
    public IReadOnlyList<FacilityReportRow> Reports { get; set; } = [];
    public string SortBy { get; set; } = "CreateDate";
    public string SortDir { get; set; } = "desc";
    public string? ScopeNote { get; set; }
}

public sealed class GenerateReportInput
{
    public string? FacilityId { get; set; }
    public bool BypassSubmission { get; set; }
    public string? Cadence { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public List<string>? ReportTypes { get; set; }
    public string? ReportTypesText { get; set; }
    public string? PatientsMode { get; set; }
    public string? Patients { get; set; }
}

public sealed class GenerateReportPage
{
    public GenerateReportInput Input { get; set; } = new();
    public IReadOnlyList<string> Measures { get; set; } = [];
    public string? MeasuresNote { get; set; }
    public string? Error { get; set; }
    public Guid? GeneratedReportId { get; set; }

    /// <summary>When automation is on, this form rejects facilities an automation run owns.</summary>
    public bool AutomationFacilitiesExcluded { get; set; }
}

public class ReportSectionPage
{
    public string? FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public string ReportId { get; set; } = string.Empty;
    public bool NotFound { get; set; }
    public string? LoadError { get; set; }
    public FacilityReportRow? Report { get; set; }
}

public sealed class ValidationResultPage : ReportSectionPage
{
    public long? IssueCount { get; set; }
    public string? Severity { get; set; }
    public string? SummaryError { get; set; }
}

public sealed class ValidationPage : ReportSectionPage
{
    public long? IssueCount { get; set; }
    public string? Severity { get; set; }
    public string? SummaryError { get; set; }
    public string? IssuesError { get; set; }
    public bool TooLarge { get; set; }
    public string? PrequalStatus { get; set; }
    public IReadOnlyList<ValidationSeverityCount> SeverityCounts { get; set; } = [];
    public IReadOnlyList<string> SeverityOptions { get; set; } = [];
    public IReadOnlyList<string> CategoryOptions { get; set; } = [];
    public ValidationIssueQuery Query { get; set; } = new();
    public IReadOnlyList<ValidationIssueRow> Issues { get; set; } = [];
    public PageBar Paging { get; set; } = new();
}

public sealed class ValidationSeverityCount
{
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class ValidationIssueQuery
{
    public string? Text { get; init; }
    public string? Severity { get; init; }
    public string? Code { get; init; }
    public string? Category { get; init; }
    public string Sort { get; init; } = "severity";
    public bool Descending { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = ReportsRules.DefaultIssuePageSize;
}

public sealed class PrequalPage : ReportSectionPage
{
    public string? Category { get; set; }
    public bool TooLarge { get; set; }
    public string? CategorizeNote { get; set; }
    public IReadOnlyList<PrequalCategoryGroup> Unacceptable { get; set; } = [];
    public IReadOnlyList<PrequalCategoryGroup> Acceptable { get; set; } = [];
    public IReadOnlyList<ValidationIssueRow> Issues { get; set; } = [];
    public int IssueTotal { get; set; }
    public bool IssuesTruncated { get; set; }
}

public sealed class PrequalCategoryGroup
{
    public string Name { get; init; } = string.Empty;
    public bool Acceptable { get; init; }
    public string Guidance { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class ValidationIssueRow
{
    public string PatientId { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Expression { get; init; } = string.Empty;
    public IReadOnlyList<ValidationCategoryRow> Categories { get; init; } = [];
}

public sealed class ValidationCategoryRow
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public bool Acceptable { get; init; }
    public string Guidance { get; init; } = string.Empty;
}

public sealed class MeasureReportPage : ReportSectionPage
{
    public string? PatientId { get; set; }
    public string? ReportingStatus { get; set; }
    public string? SubmissionStatus { get; set; }
    public IReadOnlyList<MeasureReportLine> Measures { get; set; } = [];
    public bool ShowJson { get; set; }
    public bool JsonTooLarge { get; set; }
    public string? Json { get; set; }
    public string? JsonError { get; set; }
    public bool CanDownloadJson { get; set; }
}

public sealed class MeasureReportLine
{
    public string? Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Resources { get; init; } = string.Empty;
}

public sealed class AcquisitionLogPage : ReportSectionPage
{
    public string? PatientId { get; set; }
    public string? SectionError { get; set; }
    public PageBar Paging { get; set; } = new();
    public int PageSize { get; set; } = ReportsRules.DefaultLogPageSize;
    public IReadOnlyList<AcquisitionLogRow> Logs { get; set; } = [];
}

public sealed class AcquisitionLogRow
{
    public long Id { get; init; }
    public string PatientId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Phase { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Created { get; init; } = string.Empty;
    public string Completed { get; init; } = string.Empty;
    public string Resources { get; init; } = string.Empty;
    public int Notes { get; init; }
}

public sealed class ReportsAction
{
    public ReportsAction(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    public bool Succeeded { get; }
    public string Message { get; }
}
