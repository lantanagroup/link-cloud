using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Census.Models;

public class SearchPatientEncounterModel
{
    public string FacilityId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime? Threshold { get; set; }
    public string? SortBy { get; set; }
    public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
    public int PageSize { get; set; } = 10;
    public int PageNumber { get; set; } = 1;
}