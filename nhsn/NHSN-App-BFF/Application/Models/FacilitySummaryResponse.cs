namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

public class FacilitySummaryResponse
{
    public Guid Id { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public bool IsOnboarded { get; set; }
}