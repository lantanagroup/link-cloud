namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocations;

public class FacilityLocationModel
{
    public string Id { get; set; } = "";
    public string FacilityId { get; set; } = "";
    public string LocationId { get; set; } = "";
    public string? PartOfId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAlias { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
}