namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

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
    public bool IsLowerEnvironmentTestingMode { get; set; }
}
