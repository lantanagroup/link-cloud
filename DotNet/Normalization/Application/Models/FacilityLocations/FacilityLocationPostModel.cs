namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocations;

public class FacilityLocationPostModel
{
    public string LocationId { get; set; } = "";
    public string? PartOfId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAlias { get; set; }
}