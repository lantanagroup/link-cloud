namespace Link.UI.Models;

public sealed class VendorOption
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class DispatchScheduleInput
{
    public string? Event { get; set; }
    public string? Duration { get; set; }
    public bool Remove { get; set; }
}

public sealed class FacilityEditInput
{
    public string? FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public string? TimeZone { get; set; }
    public string? VendorVersionId { get; set; }
    public string? DailyReports { get; set; }
    public string? WeeklyReports { get; set; }
    public string? MonthlyReports { get; set; }
}

public sealed class FacilityHubViewModel
{
    public bool IsCreate { get; set; }
    public bool NotFound { get; set; }
    public string? LoadError { get; set; }
    public string? FormError { get; set; }

    public string? FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public string? TimeZone { get; set; }
    public string? VendorVersionId { get; set; }
    public bool VendorListLoaded { get; set; }
    public string? VendorWarning { get; set; }
    public IReadOnlyList<VendorOption> Vendors { get; set; } = Array.Empty<VendorOption>();
    public IReadOnlyList<string> TimeZones { get; set; } = Array.Empty<string>();

    public bool DmrpEnabled { get; set; }
    public string? DailyReports { get; set; }
    public string? WeeklyReports { get; set; }
    public string? MonthlyReports { get; set; }

    public bool CensusConfigured { get; set; }
    public bool CensusExists { get; set; }
    public bool CensusEnabled { get; set; } = true;
    public string? CensusTrigger { get; set; }
    public string? CensusError { get; set; }

    public bool QueryDispatchConfigured { get; set; }
    public bool QueryDispatchExists { get; set; }
    public List<DispatchScheduleInput> Schedules { get; set; } = new();
    public string? QueryDispatchError { get; set; }
}

public sealed class FacilityWriteResult
{
    public FacilityHubViewModel? Page { get; init; }
    public string? RedirectFacilityId { get; init; }
    public string? RedirectMessage { get; init; }
    public bool RedirectToList { get; init; }

    public static FacilityWriteResult Stay(FacilityHubViewModel page) => new() { Page = page };

    public static FacilityWriteResult ToFacility(string facilityId, string message) =>
        new() { RedirectFacilityId = facilityId, RedirectMessage = message };

    public static FacilityWriteResult ToList(string message) =>
        new() { RedirectToList = true, RedirectMessage = message };
}
