namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

public class UserRoleSummaryResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? FacilityId { get; set; }
    public bool IsOnboarded { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = [];
}