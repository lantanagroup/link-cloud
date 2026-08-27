namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Session;

public class UserInfoResponse
{
    public string AccessState { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsFacilityAdmin { get; set; }
    public bool IsOnboarded { get; set; }
    public bool HasFacility { get; set; }
    public string? FacilityId { get; set; }
    public IReadOnlyCollection<string> Groups { get; set; } = [];
    public IReadOnlyCollection<string> AvailableNavigation { get; set; } = [];
    public string? AccessRequestUrl { get; set; }

    // The facility's EHR vendor, or null before step 3 captures it.
    public string? Vendor { get; set; }

    // NotStarted | InProgress | Committing | Complete | CommitFailed. IsOnboarded is this being
    // Complete, kept for the existing contract.
    public string? OnboardingStatus { get; set; }

    // Where the user resumes, or null if no step has been saved.
    public string? CurrentStepId { get; set; }

    // Which contract-pending capabilities are backed by a real adapter rather than a fixture, so
    // the UI can show an honest "not yet connected" state instead of presenting synthetic data as
    // the facility's own.
    //
    // A typed shape rather than a dictionary: PropertyNamingPolicy doesn't apply to dictionary
    // keys, so a dictionary here would serialize PascalCase keys into an otherwise camelCase
    // document.
    public CapabilitiesResponse Capabilities { get; set; } = new();
}

public class CapabilitiesResponse
{
    public bool FhirConnectionProbe { get; set; }
    public bool PatientListWithNames { get; set; }
}
