namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

public class SimulatedUserHeaderPayload
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string[] Groups { get; set; } = [];
    public string? FacilityId { get; set; }
    public string? ExternalUserId { get; set; }
}