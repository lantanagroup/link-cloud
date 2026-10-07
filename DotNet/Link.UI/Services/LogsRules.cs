using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// Log-area decisions that do not call a service: which filters are real, which rows can be
/// queued or cancelled, and which external tool links are safe to render.
/// </summary>
public static class LogsRules
{
    public const int DefaultPageSize = 10;
    public const int MaxAuditPageSize = 20;
    public const int MaxSelected = 50;
    public const int MaxAcquiredIds = 40;
    public const int ReferencePageSize = 20;
    public const int DefaultMinAgeHours = 24;
    public const int MaxMinAgeHours = 24 * 365;
    public const int MaxFilterLength = 200;

    public static readonly int[] PageSizes = [10, 20, 50];
    public static readonly int[] AuditPageSizes = [10, MaxAuditPageSize];

    public static readonly string[] Statuses =
    [
        "Pending", "Ready", "Queued", "Processing", "Completed", "Failed",
        "MaxRetriesReached", "Skipped", "ConfigurationRequired", "Cancelled",
        "ConfigurationMissing", "NotReportable"
    ];

    public static readonly string[] CancellableStatuses =
    [
        "Pending", "Ready", "Queued", "Processing", "Failed", "ConfigurationRequired"
    ];

    public static readonly string[] Phases = ["Initial", "Supplemental", "Polling", "Monitoring"];
    public static readonly string[] QueryTypes = ["Read", "Search", "SearchPost", "BulkDataRequest", "BulkDataPoll"];
    public static readonly string[] Priorities = ["Normal", "High", "Critical"];
    public static readonly string[] AcquisitionSorts =
    [
        "ExecutionDate", "CreateDate", "CompletionDate", "FacilityId", "PatientId",
        "QueryType", "QueryPhase", "Status", "Priority", "Id", "RetryAttempts"
    ];

    public static readonly string[] SftpSorts =
    [
        "ProcessDate", "ScheduledDate", "FacilityId", "Status", "AcquisitionType", "SubType", "RetryAttempts"
    ];

    public static readonly string[] AuditSorts = ["CreatedOn", "FacilityId", "Action", "ServiceName", "Resource"];
    public static readonly string[] AuditActions = ["Create", "Update", "Delete", "Query", "Submit", "Restore"];

    public static readonly (string Label, string Value)[] AuditServices =
    [
        ("Account", "Account"),
        ("Census", "Census"),
        ("Data acquisition", "DataAcquisition"),
        ("Measure evaluation", "MeasureEvaluation"),
        ("Normalization", "NormalizationService"),
        ("Query dispatch", "QueryDispatch"),
        ("Report", "Report"),
        ("Submission", "Submission"),
        ("Tenant", "Tenant"),
        ("Validation", "Validation")
    ];

    public static readonly string[] ResourceTypes =
    [
        "AllergyIntolerance", "Appointment", "AppointmentResponse", "AuditEvent", "Binary", "CarePlan", "CareTeam",
        "Condition", "Consent", "Coverage", "Device", "DeviceRequest", "DeviceUseStatement", "DiagnosticReport",
        "DocumentReference", "Encounter", "EpisodeOfCare", "Goal", "Group", "Immunization", "ImmunizationRecommendation",
        "Location", "Medication", "MedicationAdministration", "MedicationRequest", "MedicationStatement", "Observation",
        "Organization", "Patient", "Person", "Practitioner", "PractitionerRole", "Procedure", "Provenance",
        "Questionnaire", "QuestionnaireResponse", "ReferralRequest", "ServiceRequest", "RelatedPerson", "Schedule",
        "SearchParameter", "Slot", "Specimen", "StructureDefinition", "Subscription", "ValueSet", "CodeSystem"
    ];

    private static readonly HashSet<string> Terminal = new(StringComparer.OrdinalIgnoreCase)
    {
        "Completed", "MaxRetriesReached", "ConfigurationMissing", "Skipped", "Cancelled", "NotReportable"
    };

    public static int ClampPageSize(int pageSize) =>
        PageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

    public static int ClampAuditPageSize(int pageSize) =>
        pageSize == MaxAuditPageSize ? MaxAuditPageSize : DefaultPageSize;

    public static int ClampMinAge(int hours)
    {
        if (hours < 0)
            return 0;
        return hours > MaxMinAgeHours ? MaxMinAgeHours : hours;
    }

    public static bool CanProcess(string? status) =>
        !string.IsNullOrWhiteSpace(status)
        && !status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
        && !status.Equals("Skipped", StringComparison.OrdinalIgnoreCase);

    public static bool CanCancel(string? status, DateTime? created, int minAgeHours, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(status) || Terminal.Contains(status))
            return false;
        if (minAgeHours <= 0)
            return true;
        if (created is null)
            return false;

        return AsUtc(created.Value) <= utcNow.AddHours(-minAgeHours);
    }

    public static bool CanResetSftp(string? status) =>
        status is not null
        && (status.Equals("ConfigurationRequired", StringComparison.OrdinalIgnoreCase)
            || status.Equals("MaxRetriesReached", StringComparison.OrdinalIgnoreCase));

    public static bool TryExternalUrl(string? value, out string url)
    {
        url = string.Empty;
        var text = FacilityViewRules.Clean(value);
        if (text is null || !Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        url = uri.AbsoluteUri;
        return true;
    }

    public static AcquisitionSearch Prepare(AcquisitionQuery? query, bool numericOnlyFacilityId, DateTime utcNow)
    {
        query ??= new AcquisitionQuery();
        var search = new AcquisitionSearch
        {
            FacilityId = Limit(query.FacilityId),
            PatientId = Limit(query.PatientId),
            ReportId = Limit(query.ReportId),
            ResourceId = Limit(query.ResourceId),
            ResourceType = OneOf(query.ResourceType, ResourceTypes),
            QueryPhase = OneOf(query.QueryPhase, Phases),
            QueryType = OneOf(query.QueryType, QueryTypes),
            Priority = OneOf(query.Priority, Priorities),
            IncludeDeleted = query.IncludeDeleted,
            CancellableOnly = query.CancellableOnly,
            MinAgeHours = ClampMinAge(query.MinAgeHours < 0 ? DefaultMinAgeHours : query.MinAgeHours),
            SortBy = OneOf(query.SortBy, AcquisitionSorts) ?? "ExecutionDate",
            SortDir = query.SortDir?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true ? "asc" : "desc",
            Page = FacilityViewRules.ClampPage(query.Page),
            PageSize = ClampPageSize(query.PageSize),
            SearchTerm = Limit(query.SearchTerm)
        };

        var statuses = Tokens(query.Status, Statuses);
        if (search.CancellableOnly)
        {
            statuses = CancellableStatuses;
            if (search.MinAgeHours > 0)
                search.CreatedBefore = utcNow.AddHours(-search.MinAgeHours);
        }

        search.Statuses = statuses;
        search.Error = FirstError(query, search, numericOnlyFacilityId);
        search.HasFilter = HasNarrowing(search);
        return search;
    }

    public static SftpSearch PrepareSftp(SftpQuery? query, bool numericOnlyFacilityId)
    {
        query ??= new SftpQuery();
        var search = new SftpSearch
        {
            FacilityId = Limit(query.FacilityId),
            Status = OneOf(query.Status, Statuses),
            AcquisitionType = OneOf(query.AcquisitionType, FacilityAcquisitionRules.AcquisitionTypes),
            SubType = OneOf(query.SubType, FacilityAcquisitionRules.AcquisitionSubTypes),
            SortBy = OneOf(query.SortBy, SftpSorts) ?? "ProcessDate",
            SortDir = query.SortDir?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true ? "asc" : "desc",
            Page = FacilityViewRules.ClampPage(query.Page),
            PageSize = ClampPageSize(query.PageSize)
        };
        if (Overlong(query.FacilityId))
            search.Error = $"Filters must be {MaxFilterLength} characters or fewer.";
        else if (search.FacilityId is not null && !FacilityFormRules.IsValidFacilityId(search.FacilityId, numericOnlyFacilityId))
            search.Error = FacilityFormRules.FacilityIdRule(numericOnlyFacilityId);
        else if (query.Status is not null && FacilityViewRules.Clean(query.Status) is not null && search.Status is null)
            search.Error = "Status is not a known acquisition status.";
        else if (query.AcquisitionType is not null && FacilityViewRules.Clean(query.AcquisitionType) is not null && search.AcquisitionType is null)
            search.Error = "Acquisition type is not Census or Resources.";
        else if (query.SubType is not null && FacilityViewRules.Clean(query.SubType) is not null && search.SubType is null)
            search.Error = "Subtype is not a known SFTP subtype.";
        return search;
    }

    public static AuditSearch PrepareAudit(AuditQuery? query, bool numericOnlyFacilityId)
    {
        query ??= new AuditQuery();
        var search = new AuditSearch
        {
            SearchText = Limit(query.SearchText),
            FacilityId = Limit(query.FacilityId),
            CorrelationId = Limit(query.CorrelationId),
            Service = OneOf(query.Service, AuditServices.Select(item => item.Value).ToArray()),
            Action = OneOf(query.Action, AuditActions),
            User = Limit(query.User),
            SortBy = OneOf(query.SortBy, AuditSorts) ?? "CreatedOn",
            SortDir = query.SortDir?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true ? "asc" : "desc",
            Page = FacilityViewRules.ClampPage(query.Page),
            PageSize = ClampAuditPageSize(query.PageSize)
        };
        if (Overlong(query.SearchText) || Overlong(query.FacilityId) || Overlong(query.CorrelationId) || Overlong(query.User))
            search.Error = $"Filters must be {MaxFilterLength} characters or fewer.";
        else if (search.FacilityId is not null && !FacilityFormRules.IsValidFacilityId(search.FacilityId, numericOnlyFacilityId))
            search.Error = FacilityFormRules.FacilityIdRule(numericOnlyFacilityId);
        else if (query.Service is not null && FacilityViewRules.Clean(query.Service) is not null && search.Service is null)
            search.Error = "Service is not a known audit service.";
        else if (query.Action is not null && FacilityViewRules.Clean(query.Action) is not null && search.Action is null)
            search.Error = "Action is not a known audit action.";
        return search;
    }

    public static IReadOnlyList<long> ParseIds(IEnumerable<long>? ids, out string? error)
    {
        var list = (ids ?? [])
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (list.Count == 0)
        {
            error = "Select at least one log.";
            return [];
        }

        if (list.Count > MaxSelected)
        {
            error = $"Select at most {MaxSelected} logs. Use the matching-filter action to cover a larger set.";
            return [];
        }

        error = null;
        return list;
    }

    public static Dictionary<string, object> MatchingBody(AcquisitionSearch search)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal);
        Add(body, "facilityId", search.FacilityId);
        Add(body, "patientId", search.PatientId);
        Add(body, "reportId", search.ReportId);
        Add(body, "resourceId", search.ResourceId);
        Add(body, "resourceType", search.ResourceType);
        Add(body, "queryPhase", search.QueryPhase);
        Add(body, "queryType", search.QueryType);
        Add(body, "priority", search.Priority);
        Add(body, "searchTerm", search.SearchTerm);
        if (search.Statuses.Count > 0)
            body["statuses"] = search.Statuses.ToList();
        if (search.IncludeDeleted)
            body["includeDeleted"] = true;
        if (search.CreatedBefore is DateTime created)
            body["createdBefore"] = created;
        return body;
    }

    public static string NextSort(string? currentSort, string? currentDirection, string column, bool dateColumn)
    {
        if (!string.Equals(currentSort, column, StringComparison.OrdinalIgnoreCase))
            return dateColumn ? "desc" : "asc";

        return string.Equals(currentDirection, "asc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
    }

    public static string SortOrder(string? sortDir) =>
        sortDir?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true ? "Ascending" : "Descending";

    private static string? FirstError(AcquisitionQuery query, AcquisitionSearch search, bool numericOnlyFacilityId)
    {
        if (Overlong(query.FacilityId) || Overlong(query.PatientId) || Overlong(query.ReportId) || Overlong(query.ResourceId) || Overlong(query.SearchTerm))
            return $"Filters must be {MaxFilterLength} characters or fewer.";
        if (search.FacilityId is not null && !FacilityFormRules.IsValidFacilityId(search.FacilityId, numericOnlyFacilityId))
            return FacilityFormRules.FacilityIdRule(numericOnlyFacilityId);
        if (query.ResourceType is not null && FacilityViewRules.Clean(query.ResourceType) is not null && search.ResourceType is null)
            return "Resource type is not one of the acquisition resource types.";
        if (query.QueryPhase is not null && FacilityViewRules.Clean(query.QueryPhase) is not null && search.QueryPhase is null)
            return "Query phase is not a known phase.";
        if (query.QueryType is not null && FacilityViewRules.Clean(query.QueryType) is not null && search.QueryType is null)
            return "Query type is not a known type.";
        if (query.Priority is not null && FacilityViewRules.Clean(query.Priority) is not null && search.Priority is null)
            return "Priority is not Normal, High, or Critical.";
        if (UnknownStatus(query.Status))
            return "Status is not a known acquisition status.";
        return null;
    }

    private static bool HasNarrowing(AcquisitionSearch search) =>
        search.FacilityId is not null
        || search.PatientId is not null
        || search.ReportId is not null
        || search.ResourceId is not null
        || search.ResourceType is not null
        || search.QueryPhase is not null
        || search.QueryType is not null
        || search.Priority is not null
        || search.Statuses.Count > 0;

    private static bool UnknownStatus(IEnumerable<string>? values)
    {
        if (values is null)
            return false;

        foreach (var value in values)
        {
            foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var text = FacilityViewRules.Clean(part);
                if (text is not null && !Statuses.Contains(text, StringComparer.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> Tokens(IEnumerable<string>? values, IReadOnlyList<string> allowed)
    {
        if (values is null)
            return [];

        var list = new List<string>();
        foreach (var value in values)
        {
            foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var match = allowed.FirstOrDefault(item => item.Equals(part, StringComparison.OrdinalIgnoreCase));
                if (match is not null && !list.Contains(match, StringComparer.Ordinal))
                    list.Add(match);
            }
        }

        return list;
    }

    private static string? OneOf(string? value, IReadOnlyList<string> allowed)
    {
        var text = Limit(value);
        if (text is null)
            return null;
        return allowed.FirstOrDefault(item => item.Equals(text, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Limit(string? value)
    {
        var text = FacilityViewRules.Clean(value);
        if (text is null || text.Length > MaxFilterLength)
            return text is { Length: > MaxFilterLength } ? null : text;
        return text;
    }

    private static bool Overlong(string? value)
    {
        var text = FacilityViewRules.Clean(value);
        return text is { Length: > MaxFilterLength };
    }

    private static void Add(Dictionary<string, object> body, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            body[key] = value;
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
