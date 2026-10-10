using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// Facility-view decisions that do not call a service: filters, the HSLOC tree, and which report
/// actions a row is allowed to offer.
/// </summary>
public static class FacilityViewRules
{
    public const int DefaultPageSize = 10;
    public static readonly int[] PageSizes = [5, 10, 20];

    public static readonly string[] Sections = ["reports", "hsloc", "locations", "encounters", "plans"];
    public static readonly string[] ReportSorts = ["Id", "FacilityId", "ReportStartDate", "CreateDate", "Frequency", "Status", "IsDeleted"];
    public static readonly string[] LocationSorts =
    [
        "LocationMappingId", "LocationId", "LocationName", "LocationAlias", "PartOfValue", "PartOfId",
        "IsOrgLocation", "IsActive", "CreateDate", "ModifiedDate"
    ];
    public static readonly string[] EncounterSorts =
    [
        "EncounterMappingId", "EncounterId", "PatientId", "MappedToOrg", "CreateDate", "ModifiedDate"
    ];
    public static readonly string[] PlanSorts = ["measure", "dqm", "frequency", "period", "isReporting"];

    private static readonly Frequency[] FrequencyOrder =
    [
        Frequency.Daily, Frequency.Weekly, Frequency.Monthly, Frequency.Adhoc, Frequency.Discharge
    ];

    private static readonly string[] MonthNames =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    ];

    public static string NormalizeSection(string? section)
    {
        var value = Clean(section)?.ToLowerInvariant();
        return value is not null && Sections.Contains(value) ? value : "reports";
    }

    public static string? Clean(string? value)
    {
        var text = value.Sanitize().Trim();
        return text.Length == 0 ? null : text;
    }

    public static int ClampPage(int page) => page < 1 ? 1 : page;

    public static int ClampPageSize(int pageSize) =>
        PageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

    public static bool? ParseTriState(string? value)
    {
        var text = Clean(value);
        return bool.TryParse(text, out var parsed) ? parsed : null;
    }

    public static DateOnly? ParseDate(string? value)
    {
        var text = Clean(value);
        return text is not null && DateOnly.TryParse(text, out var date) ? date : null;
    }

    public static DateTime? PeriodStart(DateOnly? date) =>
        date?.ToDateTime(TimeOnly.MinValue);

    /// <summary>The end of the chosen calendar day, so a "to" date includes reports that end that day.</summary>
    public static DateTime? PeriodEnd(DateOnly? date) =>
        date?.ToDateTime(new TimeOnly(23, 59, 59));

    public static Frequency? ParseFrequency(string? value)
    {
        var text = Clean(value);
        return text is not null && Enum.TryParse<Frequency>(text, ignoreCase: true, out var frequency)
            ? frequency
            : null;
    }

    public static ScheduleStatus[] ParseStatuses(IEnumerable<string>? values)
    {
        if (values is null)
            return [];

        var parsed = new List<ScheduleStatus>();
        foreach (var value in values)
        {
            foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var text = Clean(part);
                if (text is not null
                    && Enum.TryParse<ScheduleStatus>(text, ignoreCase: true, out var status)
                    && !parsed.Contains(status))
                {
                    parsed.Add(status);
                }
            }
        }

        return parsed.ToArray();
    }

    /// <summary>
    /// A blank filter is no filter. A value that is not a report id is an error, so the list is not
    /// searched as if the filter were absent.
    /// </summary>
    public static Guid? ParseReportId(string? value, out string? error)
    {
        var text = Clean(value);
        if (text is null)
        {
            error = null;
            return null;
        }

        if (Guid.TryParse(text, out var id))
        {
            error = null;
            return id;
        }

        error = "Report ID is not a valid id.";
        return null;
    }

    public static string? AllowedSort(string? value, IReadOnlyList<string> allowed)
    {
        var text = Clean(value);
        if (text is null)
            return null;

        return allowed.FirstOrDefault(item => string.Equals(item, text, StringComparison.OrdinalIgnoreCase));
    }

    public static SortOrder? ParseSortDirection(string? value)
    {
        var text = Clean(value);
        if (text is null)
            return null;

        if (text.Equals("asc", StringComparison.OrdinalIgnoreCase)
            || text.Equals(nameof(SortOrder.Ascending), StringComparison.OrdinalIgnoreCase))
            return SortOrder.Ascending;

        if (text.Equals("desc", StringComparison.OrdinalIgnoreCase)
            || text.Equals(nameof(SortOrder.Descending), StringComparison.OrdinalIgnoreCase))
            return SortOrder.Descending;

        return null;
    }

    public static string NextSortDirection(string? currentSort, string? currentDirection, string column)
    {
        if (!string.Equals(currentSort, column, StringComparison.OrdinalIgnoreCase))
        {
            return column is "CreateDate" or "ModifiedDate" or "ReportStartDate" or "period" or "isReporting"
                ? "desc"
                : "asc";
        }

        return string.Equals(currentDirection, "asc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
    }

    public static string StatusLabel(ScheduleStatus status) => status switch
    {
        ScheduleStatus.EndOfPeriod => "End of Period",
        ScheduleStatus.CompletedNotSubmitted => "Completed (Submission Skipped)",
        _ => status.ToString()
    };

    public static string StatusBadge(ScheduleStatus status) => StatusPills.ForSchedule(status);

    /// <summary>
    /// A submitted report can be resubmitted. A test facility cannot:
    /// those reports change only inside a scenario run.
    /// </summary>
    public static bool CanResubmit(ScheduleStatus status, bool deleted, bool isTest = false) =>
        !isTest && !deleted && status is ScheduleStatus.Submitted or ScheduleStatus.CompletedNotSubmitted;

    public static bool CanAbort(ScheduleStatus status, bool deleted) =>
        !deleted && status is ScheduleStatus.New or ScheduleStatus.EndOfPeriod;

    public static bool CanCleanUp(ScheduleStatus status, bool deleted) =>
        !deleted && status is ScheduleStatus.Submitted or ScheduleStatus.CompletedNotSubmitted or ScheduleStatus.Scheduled;

    public static bool CanRestore(bool deleted) => deleted;

    public static IReadOnlyList<EnrolledCadence> Enrolled(TenantScheduledReportConfig? schedule)
    {
        schedule ??= new TenantScheduledReportConfig();
        return
        [
            new EnrolledCadence("Daily", schedule.Daily ?? []),
            new EnrolledCadence("Weekly", schedule.Weekly ?? []),
            new EnrolledCadence("Monthly", schedule.Monthly ?? [])
        ];
    }

    public static string When(DateTime value) =>
        value == default ? "" : LinkUiTime.Display(value);

    public static string When(DateTime? value) =>
        value is null || value.Value == default ? "" : LinkUiTime.Display(value.Value);

    public static IReadOnlyList<FacilityLocationNode> BuildLocationTree(IEnumerable<FacilityLocationTreeApiModel>? locations)
    {
        var nodes = new Dictionary<string, FacilityLocationNode>(StringComparer.Ordinal);
        foreach (var location in locations ?? [])
        {
            if (string.IsNullOrWhiteSpace(location.LocationId) || nodes.ContainsKey(location.LocationId))
                continue;

            nodes[location.LocationId] = new FacilityLocationNode
            {
                Id = location.Id,
                LocationId = location.LocationId,
                PartOfId = location.PartOfId,
                Name = location.LocationName,
                Alias = location.LocationAlias,
                Mapped = location.Mappings.Any(mapping => mapping.HSLOCId is not null),
                Mappings = location.Mappings.Select(mapping => new FacilityLocationMappingRow
                {
                    Id = mapping.Id,
                    CodeSystem = mapping.LocalCodeSystem,
                    LocalCode = mapping.LocalCode,
                    Hsloc = mapping.HSLOCId is null ? "Unmapped" : (mapping.HSLOCCode ?? mapping.HSLOCId.ToString()!)
                }).ToList()
            };
        }

        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in nodes.Values)
        {
            if (!string.IsNullOrWhiteSpace(node.PartOfId) && nodes.ContainsKey(node.PartOfId))
                parents[node.LocationId] = node.PartOfId;
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes.Values.ToList())
        {
            var path = new HashSet<string>(StringComparer.Ordinal);
            string? current = node.LocationId;
            while (current is not null && !visited.Contains(current))
            {
                if (!path.Add(current))
                {
                    parents.Remove(current);
                    break;
                }

                current = parents.TryGetValue(current, out var parent) ? parent : null;
            }

            visited.UnionWith(path);
        }

        var roots = new List<FacilityLocationNode>();
        foreach (var node in nodes.Values)
        {
            if (parents.TryGetValue(node.LocationId, out var parentId))
                nodes[parentId].Children.Add(node);
            else
                roots.Add(node);
        }

        return roots;
    }

    public static FacilityLocationNode? FindLocation(IEnumerable<FacilityLocationNode> roots, string? locationId)
    {
        var id = Clean(locationId);
        if (id is null)
            return null;

        var pending = new Stack<FacilityLocationNode>(roots);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (string.Equals(node.LocationId, id, StringComparison.Ordinal))
                return node;

            foreach (var child in node.Children)
                pending.Push(child);
        }

        return null;
    }

    public static IReadOnlyList<PlanOption> PeriodOptions(IEnumerable<FacilityReportingPlanModel> plans)
    {
        var byKey = new Dictionary<string, FacilityReportingPlanModel>(StringComparer.Ordinal);
        foreach (var plan in plans)
            byKey.TryAdd(PeriodKey(plan), plan);

        return byKey.Values
            .OrderByDescending(plan => plan.ReportingYear)
            .ThenByDescending(plan => plan.ReportingMonth)
            .Select(plan => new PlanOption(PeriodKey(plan), MonthLabel(plan.ReportingMonth, plan.ReportingYear)))
            .ToList();
    }

    public static IReadOnlyList<string> CadenceOptions(IEnumerable<FacilityReportingPlanModel> plans)
    {
        var present = plans
            .Where(plan => plan.Frequency is not null)
            .Select(plan => plan.Frequency!.Value)
            .Distinct()
            .ToList();

        var ordered = FrequencyOrder.Where(present.Contains).Select(frequency => frequency.ToString()).ToList();
        ordered.AddRange(present
            .Where(frequency => !FrequencyOrder.Contains(frequency))
            .Select(frequency => frequency.ToString()));
        return ordered;
    }

    public static IReadOnlyList<PlanRow> VisiblePlans(IEnumerable<FacilityReportingPlanModel> plans, FacilityViewQuery query, out PageBar paging)
    {
        var filtered = plans.Where(plan => PlanMatches(plan, query)).ToList();
        var sort = AllowedSort(query.PlanSort, PlanSorts);
        var direction = ParseSortDirection(query.PlanDir) ?? SortOrder.Ascending;
        IEnumerable<FacilityReportingPlanModel> ordered = sort is null
            ? filtered
                .OrderByDescending(plan => plan.ReportingYear)
                .ThenByDescending(plan => plan.ReportingMonth)
                .ThenBy(plan => plan.Measure ?? plan.MeasureMappingId, StringComparer.OrdinalIgnoreCase)
            : SortPlans(filtered, sort, direction);

        var pageSize = ClampPageSize(query.PageSize);
        var rows = ordered.ToList();
        var totalPages = rows.Count == 0 ? 0 : (int)Math.Ceiling(rows.Count / (double)pageSize);
        var page = ClampPage(query.Page);
        if (totalPages > 0 && page > totalPages)
            page = totalPages;

        paging = new PageBar
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = rows.Count,
            TotalPages = totalPages
        };

        return rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToPlanRow)
            .ToList();
    }

    public static string PeriodKey(FacilityReportingPlanModel plan) =>
        $"{plan.ReportingYear}-{plan.ReportingMonth}";

    public static string MonthLabel(int month, int year)
    {
        var name = month is >= 1 and <= 12 ? MonthNames[month - 1] : null;
        return name is null ? $"{month}/{year}" : $"{name} {year}";
    }

    private static bool PlanMatches(FacilityReportingPlanModel plan, FacilityViewQuery query)
    {
        var measure = Clean(query.Measure);
        if (measure is not null && !(plan.Measure ?? plan.MeasureMappingId ?? string.Empty)
                .Contains(measure, StringComparison.OrdinalIgnoreCase))
            return false;

        var dqm = Clean(query.Dqm);
        if (dqm is not null && !(plan.DQM ?? string.Empty).Contains(dqm, StringComparison.OrdinalIgnoreCase))
            return false;

        var period = Clean(query.Period);
        if (period is not null && !string.Equals(PeriodKey(plan), period, StringComparison.Ordinal))
            return false;

        var reporting = ParseTriState(query.Reporting);
        if (reporting is not null && plan.IsReporting != reporting.Value)
            return false;

        var cadence = ParseFrequency(query.Cadence);
        if (cadence is not null && plan.Frequency != cadence.Value)
            return false;

        return true;
    }

    private static IEnumerable<FacilityReportingPlanModel> SortPlans(
        List<FacilityReportingPlanModel> plans,
        string sort,
        SortOrder direction)
    {
        var descending = direction == SortOrder.Descending;
        return sort.ToLowerInvariant() switch
        {
            "measure" => Order(plans, plan => plan.Measure ?? plan.MeasureMappingId ?? string.Empty, descending),
            "dqm" => Order(plans, plan => plan.DQM ?? string.Empty, descending),
            "frequency" => Order(plans, plan => plan.Frequency?.ToString() ?? string.Empty, descending),
            "period" => descending
                ? plans.OrderByDescending(plan => plan.ReportingYear).ThenByDescending(plan => plan.ReportingMonth)
                : plans.OrderBy(plan => plan.ReportingYear).ThenBy(plan => plan.ReportingMonth),
            "isreporting" => descending
                ? plans.OrderByDescending(plan => plan.IsReporting)
                : plans.OrderBy(plan => plan.IsReporting),
            _ => plans
        };
    }

    private static IOrderedEnumerable<FacilityReportingPlanModel> Order(
        List<FacilityReportingPlanModel> plans,
        Func<FacilityReportingPlanModel, string> key,
        bool descending) =>
        descending
            ? plans.OrderByDescending(key, StringComparer.OrdinalIgnoreCase)
            : plans.OrderBy(key, StringComparer.OrdinalIgnoreCase);

    private static PlanRow ToPlanRow(FacilityReportingPlanModel plan)
    {
        var updated = plan.ModifyDate ?? plan.CreateDate;
        return new PlanRow
        {
            Measure = string.IsNullOrWhiteSpace(plan.Measure) ? plan.MeasureMappingId ?? "" : plan.Measure,
            Dqm = plan.DQM ?? "",
            Frequency = plan.Frequency?.ToString() ?? "",
            Period = MonthLabel(plan.ReportingMonth, plan.ReportingYear),
            IsReporting = plan.IsReporting,
            Updated = When(updated)
        };
    }
}
