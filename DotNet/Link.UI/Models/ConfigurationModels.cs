namespace Link.UI.Models;

public sealed class ConfigurationAction
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = "";

    public static ConfigurationAction Ok(string message) => new() { Succeeded = true, Message = message };
    public static ConfigurationAction Fail(string message) => new() { Message = message };
}

public sealed class ConfigurationHomePage
{
    public bool MeasuresConfigured { get; init; }
    public bool TenantConfigured { get; init; }
    public bool ValidationConfigured { get; init; }
    public bool AcquisitionConfigured { get; init; }
    public bool TerminologyConfigured { get; init; }
    public bool NormalizationConfigured { get; init; }
    public bool NotificationConfigured { get; init; }
    public bool DmrpEnabled { get; init; }
}

public sealed class MeasureRow
{
    public string Id { get; init; } = "";
    public string Version { get; init; } = "";
    public string Modified { get; init; } = "";
}

public sealed class MeasureListPage
{
    public bool Configured { get; init; }
    public string? LoadError { get; set; }
    public IReadOnlyList<MeasureRow> Measures { get; set; } = Array.Empty<MeasureRow>();
}

public sealed class RelatedArtifactRow
{
    public string Name { get; init; } = "";
    public string? Url { get; init; }
    public string Version { get; init; } = "";
}

public sealed class MeasureDetailPage
{
    public bool Configured { get; init; }
    public string Id { get; init; } = "";
    public string? LoadError { get; set; }
    public string Version { get; set; } = "";
    public string Created { get; set; } = "";
    public string Modified { get; set; } = "";
    public int? EntryCount { get; set; }
    public string? ArtifactError { get; set; }
    public IReadOnlyList<RelatedArtifactRow> Artifacts { get; set; } = Array.Empty<RelatedArtifactRow>();
    public IReadOnlyList<string> Libraries { get; set; } = Array.Empty<string>();
}

public sealed class MeasureCqlPage
{
    public bool Configured { get; init; }
    public string Id { get; init; } = "";
    public string? LibraryId { get; set; }
    public string? Range { get; set; }
    public string? LoadError { get; set; }
    public string? LibraryError { get; set; }
    public string? CqlError { get; set; }
    public string? Cql { get; set; }
    public bool Truncated { get; set; }
    public IReadOnlyList<string> Libraries { get; set; } = Array.Empty<string>();
}

public sealed class MeasureEvalPage
{
    public bool Configured { get; init; }
    public string Id { get; init; } = "";
    public string? Debug { get; set; }
    public string? Parameters { get; set; }
    public bool ParametersOmitted { get; set; }
    public string? LoadError { get; set; }
    public string? ResultError { get; set; }
    public string? Result { get; set; }
    public bool Truncated { get; set; }
}

public sealed class VendorRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string SecretId { get; init; } = "";
}

public sealed class VendorListPage
{
    public bool Configured { get; init; }
    public string? LoadError { get; set; }
    public IReadOnlyList<VendorRow> Vendors { get; set; } = Array.Empty<VendorRow>();
}

public sealed class VendorVersionRow
{
    public Guid Id { get; init; }
    public string Version { get; init; } = "";
}

public sealed class VendorVersionPage
{
    public bool Configured { get; init; }
    public Guid VendorId { get; init; }
    public string VendorName { get; set; } = "";
    public string? LoadError { get; set; }
    public IReadOnlyList<VendorVersionRow> Versions { get; set; } = Array.Empty<VendorVersionRow>();
}

public sealed class ValidationArtifactRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
}

public sealed class ConfigurationCategoryRow
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Severity { get; init; } = "";
    public bool Acceptable { get; init; }
    public bool Reserved { get; init; }
}

public sealed class ValidationListPage
{
    public bool Configured { get; init; }
    public string? LoadError { get; set; }
    public string? CategoryError { get; set; }
    public IReadOnlyList<ValidationArtifactRow> Artifacts { get; set; } = Array.Empty<ValidationArtifactRow>();
    public IReadOnlyList<ConfigurationCategoryRow> Categories { get; set; } = Array.Empty<ConfigurationCategoryRow>();
}

public sealed class CategoryForm
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Severity { get; set; }
    public bool Acceptable { get; set; }
    public bool Submit { get; set; } = true;
    public bool Review { get; set; } = true;
    public string? Guidance { get; set; }
}

public sealed class CategoryPage
{
    public bool Configured { get; init; }
    public bool Reserved { get; set; }
    public bool Missing { get; set; }
    public bool ShowForm { get; set; }
    public string? LoadError { get; set; }
    public string? RuleError { get; set; }
    public CategoryForm Form { get; set; } = new();
    public IReadOnlyList<CategoryRuleRow> Rules { get; set; } = Array.Empty<CategoryRuleRow>();
}

public sealed class CategoryRuleRow
{
    public long Id { get; init; }
    public string Timestamp { get; init; } = "";
    public string Summary { get; init; } = "";
    public bool Inverted { get; init; }
}

public sealed class PackageResourceRow
{
    public string ResourceType { get; init; } = "";
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public string Version { get; init; } = "";
}

public sealed class DependencyRow
{
    public string Url { get; init; } = "";
    public string Version { get; init; } = "";
    public bool ResourceExists { get; init; }
    public bool VersionExists { get; init; }
    public int SourceCount { get; init; }
}

public sealed class PackagePage
{
    public bool Configured { get; init; }
    public string Name { get; set; } = "";
    public string? LoadError { get; set; }
    public string? DependencyError { get; set; }
    public string? ListNote { get; set; }
    public string Version { get; set; } = "";
    public string Title { get; set; } = "";
    public IReadOnlyList<PackageResourceRow> Resources { get; set; } = Array.Empty<PackageResourceRow>();
    public IReadOnlyList<DependencyRow> Dependencies { get; set; } = Array.Empty<DependencyRow>();
}

public sealed class DependencyPage
{
    public bool Configured { get; init; }
    public string? LoadError { get; set; }
    public string? ListNote { get; set; }
    public IReadOnlyList<DependencyRow> Dependencies { get; set; } = Array.Empty<DependencyRow>();
}

public sealed class QueryPlanRow
{
    public string Type { get; init; } = "";
    public bool Saved { get; init; }
    public string? Error { get; init; }
}

public sealed class QueryPlanPage
{
    public bool Configured { get; init; }
    public string? FacilityId { get; set; }
    public string? LoadError { get; set; }
    public IReadOnlyList<QueryPlanRow> Plans { get; set; } = Array.Empty<QueryPlanRow>();
}

public sealed class TerminologyResourceRow
{
    public string ResourceType { get; init; } = "";
    public string Id { get; init; } = "";
    public string Url { get; init; } = "";
    public string Version { get; init; } = "";
}

public sealed class TerminologyPage
{
    public bool Configured { get; init; }
    public string? ValueSetError { get; set; }
    public string? CodeSystemError { get; set; }
    public IReadOnlyList<TerminologyResourceRow> ValueSets { get; set; } = Array.Empty<TerminologyResourceRow>();
    public IReadOnlyList<TerminologyResourceRow> CodeSystems { get; set; } = Array.Empty<TerminologyResourceRow>();
}

public sealed class CodeQuery
{
    public string? Search { get; set; }
    public string? CodeSystem { get; set; }
    public string? ValueSet { get; set; }
    public string? Version { get; set; }
    public bool ExcludeInactive { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public sealed class CodeRow
{
    public string System { get; init; } = "";
    public string Code { get; init; } = "";
    public string Display { get; init; } = "";
    public string Status { get; init; } = "";
}

public sealed class CodeSearchPage
{
    public bool Configured { get; init; }
    public CodeQuery Query { get; set; } = new();
    public bool Searched { get; set; }
    public string? LoadError { get; set; }
    public PageBar Paging { get; set; } = new();
    public IReadOnlyList<CodeRow> Codes { get; set; } = Array.Empty<CodeRow>();
}

public sealed class HslocQuery
{
    public string? Text { get; set; }
    public string? Version { get; set; }
    public int? Page { get; set; }
}

public sealed class HslocRow
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public string CdcCode { get; init; } = "";
    public string ShortDescription { get; init; } = "";
    public string LongDescription { get; init; } = "";
    public string Version { get; init; } = "";
    public bool Active { get; init; }
}

public sealed class HslocPage
{
    public bool Configured { get; init; }
    public HslocQuery Query { get; set; } = new();
    public string? LoadError { get; set; }
    public PageBar Paging { get; set; } = new();
    public IReadOnlyList<HslocRow> Rows { get; set; } = Array.Empty<HslocRow>();
}

public sealed class OperationQuery
{
    public string? FacilityId { get; set; }
    public string? OperationType { get; set; }
    public string? ResourceType { get; set; }
    public string? OperationId { get; set; }
    public string? VendorVersionId { get; set; }
    public string? EditType { get; set; }
    public string? EditId { get; set; }
    public bool IncludeDisabled { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public sealed class OperationRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string OperationType { get; init; } = "";
    public string FacilityId { get; init; } = "";
    public bool Disabled { get; init; }
    public string Resources { get; init; } = "";
    public IReadOnlyList<Guid> VendorVersionIds { get; init; } = Array.Empty<Guid>();
}

public sealed class OperationSearchPage
{
    public bool Configured { get; init; }
    public OperationQuery Query { get; set; } = new();
    public string? LoadError { get; set; }
    public string? ResourceWarning { get; set; }
    public PageBar Paging { get; set; } = new();
    public IReadOnlyList<string> ResourceTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<OperationRow> Operations { get; set; } = Array.Empty<OperationRow>();
    public FacilityHubViewModel? VendorEditor { get; set; }
    public string? ActionMessage { get; set; }
}

public sealed class NotificationQuery
{
    public string? SearchText { get; set; }
    public string? FacilityId { get; set; }
    public string? NotificationType { get; set; }
    public string? CreatedOnStart { get; set; }
    public string? CreatedOnEnd { get; set; }
    public string? SentOnStart { get; set; }
    public string? SentOnEnd { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public sealed class NotificationRow
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "";
    public string FacilityId { get; init; } = "";
    public string Subject { get; init; } = "";
    public string CreatedOn { get; init; } = "";
}

public sealed class NotificationSendForm
{
    public string? NotificationType { get; set; }
    public string? FacilityId { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string? Recipients { get; set; }
    public string? Bcc { get; set; }
}

public sealed class NotificationListPage
{
    public bool Configured { get; init; }
    public NotificationQuery Query { get; set; } = new();
    public string? LoadError { get; set; }
    public PageBar Paging { get; set; } = new();
    public IReadOnlyList<NotificationRow> Notifications { get; set; } = Array.Empty<NotificationRow>();
}

public sealed class NotificationDetailPage
{
    public bool Configured { get; init; }
    public string? LoadError { get; set; }
    public string Id { get; init; } = "";
    public string Type { get; set; } = "";
    public string FacilityId { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Recipients { get; set; } = "";
    public string Bcc { get; set; } = "";
    public string CreatedOn { get; set; } = "";
    public string SentOn { get; set; } = "";
}

public sealed class NotificationConfigQuery
{
    public string? SearchText { get; set; }
    public string? FacilityId { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? Edit { get; set; }
}

public sealed class NotificationConfigForm
{
    public string? Id { get; set; }
    public string? FacilityId { get; set; }
    public string? Emails { get; set; }
    public bool EmailEnabled { get; set; } = true;
}

public sealed class NotificationConfigRow
{
    public string Id { get; init; } = "";
    public string FacilityId { get; init; } = "";
    public string Emails { get; init; } = "";
    public bool EmailEnabled { get; init; }
}

public sealed class NotificationConfigPage
{
    public bool Configured { get; init; }
    public NotificationConfigQuery Query { get; set; } = new();
    public NotificationConfigForm Form { get; set; } = new();
    public string? LoadError { get; set; }
    public string? FormError { get; set; }
    public PageBar Paging { get; set; } = new();
    public IReadOnlyList<NotificationConfigRow> Configurations { get; set; } = Array.Empty<NotificationConfigRow>();
}

public sealed class MappingQuery
{
    public string? Measure { get; set; }
    public string? Dqm { get; set; }
    public string? Frequency { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public sealed class MappingForm
{
    public string? Id { get; set; }
    public string? Measure { get; set; }
    public string? Dqm { get; set; }
    public string? Frequency { get; set; }
}

public sealed class MappingRow
{
    public string Id { get; init; } = "";
    public string Measure { get; init; } = "";
    public string Dqm { get; init; } = "";
    public string Frequency { get; init; } = "";
}

public sealed class MappingPage
{
    public bool Enabled { get; init; }
    public bool Configured { get; init; }
    public MappingQuery Query { get; set; } = new();
    public string? LoadError { get; set; }
    public PageBar Paging { get; set; } = new();
    public IReadOnlyList<MappingRow> Mappings { get; set; } = Array.Empty<MappingRow>();
}
