using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using Link.UI.Services;

namespace Link.UI.Models;

public sealed class FacilityViewQuery
{
    public string? Section { get; set; }
    public string? ReportId { get; set; }
    public List<string>? Status { get; set; }
    public string? Frequency { get; set; }
    public string? Created { get; set; }
    public string? PeriodFrom { get; set; }
    public string? PeriodTo { get; set; }
    public bool ShowDeleted { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = FacilityViewRules.DefaultPageSize;

    public string? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAlias { get; set; }
    public string? PartOf { get; set; }
    public string? OrgLocation { get; set; }
    public bool ShowInactive { get; set; }

    public string? EncounterId { get; set; }
    public string? PatientId { get; set; }
    public string? Mapped { get; set; }
    public int? Location { get; set; }

    public string? Hsloc { get; set; }

    public string? Measure { get; set; }
    public string? Dqm { get; set; }
    public string? Period { get; set; }
    public string? Cadence { get; set; }
    public string? Reporting { get; set; }
    public string? PlanSort { get; set; }
    public string? PlanDir { get; set; }

    public Dictionary<string, string> SectionRoute(string section) =>
        new(StringComparer.Ordinal) { ["section"] = section };

    public Dictionary<string, string> ToRoute(int? page = null, string? sortBy = null, string? sortDir = null)
    {
        var route = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["section"] = string.IsNullOrWhiteSpace(Section) ? "reports" : Section
        };
        Add(route, "reportId", ReportId);
        if (Status is { Count: > 0 })
            route["status"] = string.Join(",", Status.Where(value => !string.IsNullOrWhiteSpace(value)));
        Add(route, "frequency", Frequency);
        Add(route, "created", Created);
        Add(route, "periodFrom", PeriodFrom);
        Add(route, "periodTo", PeriodTo);
        if (ShowDeleted)
            route["showDeleted"] = "true";
        Add(route, "sortBy", sortBy ?? SortBy);
        Add(route, "sortDir", sortDir ?? SortDir);
        Add(route, "locationId", LocationId);
        Add(route, "locationName", LocationName);
        Add(route, "locationAlias", LocationAlias);
        Add(route, "partOf", PartOf);
        Add(route, "orgLocation", OrgLocation);
        if (ShowInactive)
            route["showInactive"] = "true";
        Add(route, "encounterId", EncounterId);
        Add(route, "patientId", PatientId);
        Add(route, "mapped", Mapped);
        if (Location is > 0)
            route["location"] = Location.Value.ToString();
        Add(route, "hsloc", Hsloc);
        Add(route, "measure", Measure);
        Add(route, "dqm", Dqm);
        Add(route, "period", Period);
        Add(route, "cadence", Cadence);
        Add(route, "reporting", Reporting);
        Add(route, "planSort", PlanSort);
        Add(route, "planDir", PlanDir);

        var size = pageSizeOrDefault();
        if (size != FacilityViewRules.DefaultPageSize)
            route["pageSize"] = size.ToString();

        var number = page ?? (Page < 1 ? 1 : Page);
        if (number > 1)
            route["page"] = number.ToString();

        return route;
    }

    private int pageSizeOrDefault() =>
        FacilityViewRules.PageSizes.Contains(PageSize) ? PageSize : FacilityViewRules.DefaultPageSize;

    private static void Add(Dictionary<string, string> route, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            route[key] = value;
    }
}

public sealed class ReportPageQuery
{
    public string? PatientId { get; set; }
    public string? Patient { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = FacilityViewRules.DefaultPageSize;

    public Dictionary<string, string> ToRoute(string reportId, int? page = null, string? patient = null)
    {
        var route = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["reportId"] = reportId
        };
        if (!string.IsNullOrWhiteSpace(PatientId))
            route["patientId"] = PatientId;
        var selected = patient ?? Patient;
        if (!string.IsNullOrWhiteSpace(selected))
            route["patient"] = selected;

        var size = FacilityViewRules.PageSizes.Contains(PageSize) ? PageSize : FacilityViewRules.DefaultPageSize;
        if (size != FacilityViewRules.DefaultPageSize)
            route["pageSize"] = size.ToString();

        var number = page ?? (Page < 1 ? 1 : Page);
        if (number > 1)
            route["page"] = number.ToString();

        return route;
    }
}

public sealed class FacilityViewModel
{
    public string? FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public bool NotFound { get; set; }
    public bool DmrpEnabled { get; set; }
    public string? LoadError { get; set; }
    public string Section { get; set; } = "reports";
    public FacilityViewQuery Query { get; set; } = new();
    public IReadOnlyList<EnrolledCadence> Enrolled { get; set; } = [];

    public string? SectionError { get; set; }
    public string? SectionNote { get; set; }
    public PageBar Paging { get; set; } = new();

    public IReadOnlyList<FacilityReportRow> Reports { get; set; } = [];
    public IReadOnlyList<LocationRow> Locations { get; set; } = [];
    public LocationRow? LocationDetail { get; set; }
    public string? LocationDetailError { get; set; }
    public IReadOnlyList<EncounterRow> Encounters { get; set; } = [];
    public IReadOnlyList<FacilityLocationNode> LocationTree { get; set; } = [];
    public FacilityLocationNode? SelectedLocation { get; set; }
    public IReadOnlyList<PlanRow> Plans { get; set; } = [];
    public IReadOnlyList<PlanOption> Periods { get; set; } = [];
    public IReadOnlyList<string> Cadences { get; set; } = [];
}

public sealed class ReportDetailModel
{
    public string? FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public bool NotFound { get; set; }
    public string? LoadError { get; set; }
    public string ReportId { get; set; } = string.Empty;
    public ReportPageQuery Query { get; set; } = new();
    public FacilityReportRow? Report { get; set; }
    public string? SectionError { get; set; }
    public PageBar Paging { get; set; } = new();
    public IReadOnlyList<ReportPatientRow> Patients { get; set; } = [];
    public ReportPatientDetail? Patient { get; set; }
}

public sealed class EnrolledCadence
{
    public EnrolledCadence(string cadence, IReadOnlyList<string> measures)
    {
        Cadence = cadence;
        Measures = measures;
    }

    public string Cadence { get; }
    public IReadOnlyList<string> Measures { get; }
}

public sealed class PageBar
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = FacilityViewRules.DefaultPageSize;
    public long TotalCount { get; init; }
    public long TotalPages { get; init; }
    public bool HasPrevious => Page > 1 && TotalPages > 0;
    public bool HasNext => TotalPages > 0 && Page < TotalPages;
}

public sealed class FacilityReportRow
{
    public Guid Id { get; init; }
    public string FacilityId { get; init; } = string.Empty;
    public string Frequency { get; init; } = string.Empty;
    public string Measures { get; init; } = string.Empty;
    public string PeriodStart { get; init; } = string.Empty;
    public string PeriodEnd { get; init; } = string.Empty;
    public string Created { get; init; } = string.Empty;
    public string? Submitted { get; init; }
    public ScheduleStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public bool Deleted { get; init; }
    public int? CensusCount { get; init; }
    public int? InitialPopulationCount { get; init; }
    public bool CanResubmit { get; init; }
    public bool CanAbort { get; init; }
    public bool CanCleanUp { get; init; }
    public bool CanRestore { get; init; }
}

public sealed class LocationRow
{
    public int Id { get; init; }
    public string? LocationId { get; init; }
    public string? Name { get; init; }
    public string? Alias { get; init; }
    public string? PartOf { get; init; }
    public int? PartOfId { get; init; }
    public bool IsOrg { get; init; }
    public bool IsActive { get; init; }
    public string Created { get; init; } = string.Empty;
    public string Modified { get; init; } = string.Empty;
}

public sealed class EncounterLocationLink
{
    public string? LocationId { get; init; }
    public int MappingId { get; init; }
}

public sealed class EncounterRow
{
    public int Id { get; init; }
    public string EncounterId { get; init; } = string.Empty;
    public string PatientId { get; init; } = string.Empty;
    public bool MappedToOrg { get; init; }
    public string Created { get; init; } = string.Empty;
    public string Modified { get; init; } = string.Empty;
    public IReadOnlyList<EncounterLocationLink> Locations { get; init; } = [];
}

public sealed class FacilityLocationNode
{
    public string Id { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public string? PartOfId { get; init; }
    public string? Name { get; init; }
    public string? Alias { get; init; }
    public bool Mapped { get; init; }
    public IReadOnlyList<FacilityLocationMappingRow> Mappings { get; init; } = [];
    public List<FacilityLocationNode> Children { get; init; } = [];
}

public sealed class FacilityLocationMappingRow
{
    public string Id { get; init; } = string.Empty;
    public string CodeSystem { get; init; } = string.Empty;
    public string LocalCode { get; init; } = string.Empty;
    public string Hsloc { get; init; } = string.Empty;
}

public sealed class PlanOption
{
    public PlanOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }
    public string Label { get; }
}

public sealed class PlanRow
{
    public string Measure { get; init; } = string.Empty;
    public string Dqm { get; init; } = string.Empty;
    public string Frequency { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public bool IsReporting { get; init; }
    public string Updated { get; init; } = string.Empty;
}

public sealed class ReportPatientRow
{
    public string PatientId { get; init; } = string.Empty;
    public string ReportingStatus { get; init; } = string.Empty;
    public string SubmissionStatus { get; init; } = string.Empty;
    public string LocationOrg { get; init; } = string.Empty;
    public string EncounterMapping { get; init; } = string.Empty;
    public string Hsloc { get; init; } = string.Empty;
}

public sealed class ReportPatientDetail
{
    public string PatientId { get; init; } = string.Empty;
    public string? Error { get; init; }
    public bool AcquisitionMissing { get; init; }
    public int EncounterCount { get; init; }
    public int OrgEncounterCount { get; init; }
    public int AssumedOrgEncounterCount { get; init; }
    public IReadOnlyList<string> Locations { get; init; } = [];
    public bool NormalizationMissing { get; init; }
    public IReadOnlyList<string> CodeMaps { get; init; } = [];
}

public readonly record struct FacilityViewAction(bool Succeeded, string Message);
