namespace Link.UI.Models;

public sealed class SystemAction
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = "";

    public static SystemAction Ok(string message) => new() { Succeeded = true, Message = message };
    public static SystemAction Fail(string message) => new() { Message = message };
}

public sealed class SystemHomePage
{
    public bool AccountConfigured { get; init; }
    public bool AdminConfigured { get; init; }
}

public sealed class UserQuery
{
    public string? SearchText { get; set; }
    public string? FacilityId { get; set; }
    public string? Role { get; set; }
    public string? Claim { get; set; }
    public bool IncludeDeactivated { get; set; }
    public bool IncludeDeleted { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class UserForm
{
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public List<string>? Roles { get; set; }
}

public sealed class AccountUserInput
{
    public string Username { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string? MiddleName { get; init; }
    public string LastName { get; init; } = "";
    public string Email { get; init; } = "";
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}

public sealed class UserRow
{
    public Guid Id { get; init; }
    public string Username { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Email { get; init; } = "";
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public bool IsDeleted { get; init; }
    public bool IsActive { get; init; }
}

public sealed class UserListPage
{
    public bool Configured { get; set; }
    public string? LoadError { get; set; }
    public UserQuery Query { get; set; } = new();
    public IReadOnlyList<UserRow> Users { get; set; } = Array.Empty<UserRow>();
    public PageBar Paging { get; set; } = new();
}

public sealed class UserEditPage
{
    public bool Configured { get; set; }
    public bool ReadOnly { get; set; }
    public bool Found { get; set; }
    public string? LoadError { get; set; }
    public string? ClaimsError { get; set; }
    public Guid? Id { get; set; }
    public UserForm Form { get; set; } = new();
    public IReadOnlyList<string> KnownRoles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ClaimCatalog { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Claims { get; set; } = Array.Empty<string>();
    public bool IsDeleted { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ClaimForm
{
    public List<string>? Claims { get; set; }
}

public sealed class RoleForm
{
    public Guid? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public sealed class RoleRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<string> Claims { get; init; } = Array.Empty<string>();
}

public sealed class RoleListPage
{
    public bool Configured { get; set; }
    public string? LoadError { get; set; }
    public string? ClaimsError { get; set; }
    public IReadOnlyList<string> ClaimCatalog { get; set; } = Array.Empty<string>();
    public IReadOnlyList<RoleRow> Roles { get; set; } = Array.Empty<RoleRow>();
}

public sealed class HealthEntryRow
{
    public string Name { get; init; } = "";
    public string Status { get; init; } = "";
    public string Duration { get; init; } = "";
    public string Description { get; init; } = "";
}

public sealed class HealthRow
{
    public string Service { get; init; } = "";
    public string Status { get; init; } = "";
    public string Duration { get; init; } = "";
    public IReadOnlyList<HealthEntryRow> Entries { get; init; } = Array.Empty<HealthEntryRow>();
    public int HiddenEntries { get; init; }
}

public sealed class ServiceInfoRow
{
    public string ServiceName { get; init; } = "";
    public string Version { get; init; } = "";
    public string ProductVersion { get; init; } = "";
    public string Commit { get; init; } = "";
    public string Build { get; init; } = "";
}

public sealed class HealthPage
{
    public bool Configured { get; set; }
    public string? Service { get; set; }
    public string? HealthError { get; set; }
    public string? InfoError { get; set; }
    public IReadOnlyList<HealthRow> Reports { get; set; } = Array.Empty<HealthRow>();
    public IReadOnlyList<ServiceInfoRow> Services { get; set; } = Array.Empty<ServiceInfoRow>();
}

public sealed class ConfiguredAddress
{
    public string Label { get; init; } = "";
    public string Url { get; init; } = "";
}

public sealed class AppConfigurationPage
{
    public IReadOnlyList<ConfiguredAddress> Addresses { get; init; } = Array.Empty<ConfiguredAddress>();
    public string Missing { get; init; } = "";
}

public sealed class ReportScheduledForm
{
    public string? FacilityId { get; set; }
    public string? Frequency { get; set; }
    public string? ReportTypes { get; set; }
    public string? StartDate { get; set; }
    public string? DelayMinutes { get; set; }
    public string? ReportTrackingId { get; set; }
}

public sealed class ReportScheduledRequest
{
    public string FacilityId { get; init; } = "";
    public string Frequency { get; init; } = "";
    public IReadOnlyList<string> ReportTypes { get; init; } = Array.Empty<string>();
    public DateTime StartDateUtc { get; init; }
    public int DelayMinutes { get; init; }
    public Guid? ReportTrackingId { get; init; }
}

public sealed class PatientListForm
{
    public string? FacilityId { get; set; }
    public string? ListType { get; set; }
    public string? TimeFrame { get; set; }
    public string? PatientIds { get; set; }
    public string? ReportTrackingId { get; set; }
}

public sealed class PatientListRequest
{
    public string FacilityId { get; init; } = "";
    public string ListType { get; init; } = "";
    public string TimeFrame { get; init; } = "";
    public IReadOnlyList<string> PatientIds { get; init; } = Array.Empty<string>();
    public Guid? ReportTrackingId { get; init; }
}

public sealed class IntegrationPage
{
    public bool Configured { get; set; }
    public bool NumericOnlyFacilityId { get; set; }
    public string? ReadError { get; set; }
    public string? ReadNote { get; set; }
    public IReadOnlyList<ConsumerTopicRow> Topics { get; set; } = Array.Empty<ConsumerTopicRow>();
}

public sealed class ConsumerTopicRow
{
    public string Topic { get; init; } = "";
    public IReadOnlyList<ConsumerEventRow> Events { get; init; } = Array.Empty<ConsumerEventRow>();
}

public sealed class ConsumerEventRow
{
    public string CorrelationId { get; init; } = "";
    public string PatientId { get; init; } = "";
    public string Error { get; init; } = "";
}

public sealed class PatientEventForm
{
    public string? FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? EventType { get; set; }
}

public sealed class PatientEventRequest
{
    public string FacilityId { get; init; } = "";
    public string PatientId { get; init; } = "";
    public string EventType { get; init; } = "";
}

public sealed class DataAcquisitionForm
{
    public string? FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? QueryType { get; set; }
    public string? ReportTypes { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}

public sealed class DataAcquisitionRequest
{
    public string FacilityId { get; init; } = "";
    public string PatientId { get; init; } = "";
    public string QueryType { get; init; } = "";
    public IReadOnlyList<string> ReportTypes { get; init; } = Array.Empty<string>();
    public DateTime StartDateUtc { get; init; }
    public DateTime EndDateUtc { get; init; }
}

public sealed class PatientAcquiredForm
{
    public string? FacilityId { get; set; }
    public string? PatientIds { get; set; }
    public string? ReportTrackingId { get; set; }
}

public sealed class PatientAcquiredRequest
{
    public string FacilityId { get; init; } = "";
    public IReadOnlyList<string> PatientIds { get; init; } = Array.Empty<string>();
    public Guid? ReportTrackingId { get; init; }
}
