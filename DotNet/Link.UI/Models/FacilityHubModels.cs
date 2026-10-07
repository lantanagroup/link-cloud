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

    public bool NormalizationConfigured { get; set; }
    public NormalizationPanel Normalization { get; set; } = new();
    public string? NormalizationError { get; set; }

    public FacilityNotificationSection Notification { get; set; } = new();
}

public sealed class FacilityNotificationSection
{
    public bool Configured { get; set; }
    public bool Exists { get; set; }
    public string? Id { get; set; }
    public string Emails { get; set; } = "";
    public bool EmailEnabled { get; set; } = true;
    public string? Error { get; set; }
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
    public string? RedirectSequenceType { get; init; }
    public int? RedirectOperationPage { get; init; }

    public static FacilityWriteResult ToFacility(string facilityId, string message) =>
        new() { RedirectFacilityId = facilityId, RedirectMessage = message };

    public static FacilityWriteResult ToFacility(
        string facilityId,
        string message,
        string? planType,
        int? reportingOrgId,
        string? sequenceType = null,
        int? operationPage = null) =>
        new()
        {
            RedirectFacilityId = facilityId,
            RedirectMessage = message,
            RedirectPlanType = planType,
            RedirectReportingOrgId = reportingOrgId,
            RedirectSequenceType = sequenceType,
            RedirectOperationPage = operationPage
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

public sealed class NormalizationPanel
{
    public bool ReadFailed { get; set; }
    public string? ResourceWarning { get; set; }
    public IReadOnlyList<string> ResourceTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<NormalizationOperationRow> Operations { get; set; } = Array.Empty<NormalizationOperationRow>();
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long TotalCount { get; set; }
    public long TotalPages { get; set; }
    public bool EditorOpen { get; set; }
    public NormalizationOperationInput Editor { get; set; } = new();
    public string? SequenceType { get; set; }
    public IReadOnlyList<string> SequenceResourceTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<NormalizationSequenceEntryInput> Sequence { get; set; } = Array.Empty<NormalizationSequenceEntryInput>();
    public bool SequenceIncomplete { get; set; }
    public string? TestResult { get; set; }
    public bool TestFailed { get; set; }
    public bool VendorOwned { get; set; }
    public IReadOnlyList<string> ImportVendorIds { get; set; } = Array.Empty<string>();
}

public sealed class NormalizationOperationRow
{
    public Guid Id { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
    public string ResourceTypes { get; set; } = string.Empty;
    public string OperationJson { get; set; } = string.Empty;
}

public sealed class NormalizationOperationInput
{
    public string? OperationId { get; set; }
    public string? OperationType { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsDisabled { get; set; }
    public bool ParseFailed { get; set; }
    public List<string>? ResourceTypes { get; set; }
    public string? SourceFhirPath { get; set; }
    public string? TargetFhirPath { get; set; }
    public string? FhirPath { get; set; }
    public string? TargetValue { get; set; }
    public int? MaxIterations { get; set; }
    public bool SplitOnComma { get; set; }
    public List<ConditionInput>? Conditions { get; set; }
    public List<CodeSystemMapInput>? Maps { get; set; }
    public List<ExtensionUrlInput>? ExtensionUrls { get; set; }
    public string? TestResource { get; set; }
    public bool VendorPresetsPosted { get; set; }
    public List<string>? VendorVersionIds { get; set; }
}

public sealed class ConditionInput
{
    public string? FhirPathSource { get; set; }
    public int Operator { get; set; }
    public string? Value { get; set; }
    public bool Remove { get; set; }
}

public sealed class CodeSystemMapInput
{
    public string? SourceSystem { get; set; }
    public string? TargetSystem { get; set; }
    public bool Remove { get; set; }
    public List<CodeMapEntryInput>? Entries { get; set; }

    /// <summary>CSV or TSV rows appended to <see cref="Entries"/> when the operation is saved.</summary>
    public string? PasteRows { get; set; }
}

public sealed class CodeMapEntryInput
{
    public string? SourceCode { get; set; }
    public string? Code { get; set; }
    public string? Display { get; set; }
    public bool Remove { get; set; }
}

public sealed class ExtensionUrlInput
{
    public string? Url { get; set; }
    public bool Remove { get; set; }
}

public sealed class NormalizationSequenceEntryInput
{
    public Guid OperationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
    public int? Sequence { get; set; }
}

public sealed class NormalizationSequenceInput
{
    public string? ResourceType { get; set; }
    public List<NormalizationSequenceEntryInput>? Rows { get; set; }
}
