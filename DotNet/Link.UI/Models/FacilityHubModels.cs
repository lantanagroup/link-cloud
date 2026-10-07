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

    public bool DataAcquisitionConfigured { get; set; }
    public FhirQueryPanel FhirQuery { get; set; } = new();
    public string? FhirQueryError { get; set; }
    public FhirListPanel FhirList { get; set; } = new();
    public string? FhirListError { get; set; }
    public QueryPlanPanel QueryPlan { get; set; } = new();
    public string? QueryPlanError { get; set; }
    public IReadOnlyList<string> ExistingQueryPlanTypes { get; set; } = Array.Empty<string>();
    public ReportingOrgPanel ReportingOrg { get; set; } = new();
    public string? ReportingOrgError { get; set; }
    public SftpPanel Sftp { get; set; } = new();
    public string? SftpError { get; set; }
}

public sealed class FacilityWriteResult
{
    public FacilityHubViewModel? Page { get; init; }
    public string? RedirectFacilityId { get; init; }
    public string? RedirectMessage { get; init; }
    public bool RedirectToList { get; init; }

    public static FacilityWriteResult Stay(FacilityHubViewModel page) => new() { Page = page };

    public string? RedirectPlanType { get; init; }
    public int? RedirectReportingOrgId { get; init; }

    public static FacilityWriteResult ToFacility(string facilityId, string message) =>
        new() { RedirectFacilityId = facilityId, RedirectMessage = message };

    public static FacilityWriteResult ToFacility(string facilityId, string message, string? planType, int? reportingOrgId) =>
        new()
        {
            RedirectFacilityId = facilityId,
            RedirectMessage = message,
            RedirectPlanType = planType,
            RedirectReportingOrgId = reportingOrgId
        };

    public static FacilityWriteResult ToList(string message) =>
        new() { RedirectToList = true, RedirectMessage = message };
}

public sealed class HeaderInput
{
    public string? Key { get; set; }
    public string? Value { get; set; }
    public bool Remove { get; set; }
}

public sealed class FhirQueryPanel
{
    public bool Exists { get; set; }
    public bool ReadFailed { get; set; }
    public string? Id { get; set; }
    public string? FhirServerBaseUrl { get; set; }
    public int? MaxConcurrentRequests { get; set; } = 1;
    public int? MaxRetries { get; set; }
    public string? MinPull { get; set; }
    public string? MaxPull { get; set; }
    public bool AuthEnabled { get; set; }
    public string? AuthType { get; set; }
    public string? AuthKey { get; set; }
    public string? TokenUrl { get; set; }
    public string? Audience { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Scope { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public List<HeaderInput> CustomHeaders { get; set; } = new();

    public void Apply(FhirQueryPanel posted)
    {
        Exists = posted.Exists;
        Id = posted.Id;
        FhirServerBaseUrl = posted.FhirServerBaseUrl;
        MaxConcurrentRequests = posted.MaxConcurrentRequests;
        MaxRetries = posted.MaxRetries;
        MinPull = posted.MinPull;
        MaxPull = posted.MaxPull;
        AuthEnabled = posted.AuthEnabled;
        AuthType = posted.AuthType;
        AuthKey = posted.AuthKey;
        TokenUrl = posted.TokenUrl;
        Audience = posted.Audience;
        ClientId = posted.ClientId;
        ClientSecret = posted.ClientSecret;
        Scope = posted.Scope;
        UserName = posted.UserName;
        Password = posted.Password;
        CustomHeaders = posted.CustomHeaders ?? new List<HeaderInput>();
    }
}

public sealed class PatientListInput
{
    public string? Status { get; set; }
    public string? TimeFrame { get; set; }
    public string? FhirId { get; set; }
}

public sealed class FhirListPanel
{
    public bool Exists { get; set; }
    public bool ReadFailed { get; set; }
    public string? FhirBaseServerUrl { get; set; }
    public List<PatientListInput> Lists { get; set; } = new();

    public void Apply(FhirListPanel posted)
    {
        Exists = posted.Exists;
        FhirBaseServerUrl = posted.FhirBaseServerUrl;
        Lists = posted.Lists ?? new List<PatientListInput>();
    }
}

public sealed class QueryParameterInput
{
    public string? Name { get; set; }
    public string? ParameterType { get; set; }
    public string? Literal { get; set; }
    public string? Variable { get; set; }
    public string? Format { get; set; }
    public string? Resource { get; set; }
    public string? Paged { get; set; }
    public bool Remove { get; set; }
}

public sealed class QueryRowInput
{
    public string? ResourceType { get; set; }
    public string? QueryConfigType { get; set; } = "Parameter";
    public string? OperationType { get; set; } = "Search";
    public int? Paged { get; set; } = 100;
    public bool Remove { get; set; }
    public List<QueryParameterInput> Parameters { get; set; } = new();
}

public sealed class QueryPlanPanel
{
    public bool Exists { get; set; }
    public bool ReadFailed { get; set; }
    public string? Type { get; set; } = "Discharge";
    public string? PlanName { get; set; }
    public string? EhrDescription { get; set; }
    public string? LookBack { get; set; }
    public List<QueryRowInput> InitialQueries { get; set; } = new();
    public List<QueryRowInput> SupplementalQueries { get; set; } = new();

    public void Apply(QueryPlanPanel posted)
    {
        Exists = posted.Exists;
        Type = posted.Type;
        PlanName = posted.PlanName;
        EhrDescription = posted.EhrDescription;
        LookBack = posted.LookBack;
        InitialQueries = posted.InitialQueries ?? new List<QueryRowInput>();
        SupplementalQueries = posted.SupplementalQueries ?? new List<QueryRowInput>();
    }
}

public sealed class ReportingMatchInput
{
    public string? IdentifierSystem { get; set; }
    public string? IdentifierCode { get; set; }
    public string? OrganizationId { get; set; }
    public string? LocationTypeCode { get; set; }
    public string? LocationAlias { get; set; }
    public bool Remove { get; set; }
}

public sealed class ReportingOrgChoice
{
    public int Id { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ReportingOrgPanel
{
    public bool Exists { get; set; }
    public bool ReadFailed { get; set; }
    public int? ConfigId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string? SetupMethod { get; set; } = "manual";
    public string? FhirPath { get; set; }
    public List<ReportingMatchInput> Matches { get; set; } = new();
    public List<ReportingOrgChoice> Choices { get; set; } = new();

    public void Apply(ReportingOrgPanel posted)
    {
        Exists = posted.Exists;
        ConfigId = posted.ConfigId;
        Description = posted.Description;
        IsActive = posted.IsActive;
        SetupMethod = posted.SetupMethod;
        FhirPath = posted.FhirPath;
        Matches = posted.Matches ?? new List<ReportingMatchInput>();
    }
}

public sealed class SftpAcquisitionInput
{
    public string? AcquisitionType { get; set; }
    public string? SubType { get; set; } = "None";
    public string? RemoteDirectory { get; set; }
    public string? ProcessedDirectory { get; set; }
    public string? FileNamePattern { get; set; }
    public bool Remove { get; set; }
}

public sealed class SftpPanel
{
    public bool Exists { get; set; }
    public bool ReadFailed { get; set; }
    public string? ConfigurationId { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; } = 22;
    public string? RemoteDirectory { get; set; } = "/";
    public string? Timeout { get; set; } = "00:01:00";
    public bool RemoveAfterProcessing { get; set; }
    public bool EnableBenchmarking { get; set; }
    public bool? CredentialsSaved { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public List<SftpAcquisitionInput> Acquisitions { get; set; } = new();

    public void Apply(SftpPanel posted)
    {
        Exists = posted.Exists;
        ConfigurationId = posted.ConfigurationId;
        Host = posted.Host;
        Port = posted.Port;
        RemoteDirectory = posted.RemoteDirectory;
        Timeout = posted.Timeout;
        RemoveAfterProcessing = posted.RemoveAfterProcessing;
        EnableBenchmarking = posted.EnableBenchmarking;
        Username = posted.Username;
        Password = null;
        Acquisitions = posted.Acquisitions ?? new List<SftpAcquisitionInput>();
    }
}
