namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

public class UserInfoResponse
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Roles { get; set; } = [];
    public bool IsSystemAdmin { get; set; }
    public bool IsOnboarded { get; set; }
    public string? FacilityId { get; set; }
    public IReadOnlyCollection<string> Groups { get; set; } = [];
    public IReadOnlyCollection<string> AvailableNavigation { get; set; } = [];
}