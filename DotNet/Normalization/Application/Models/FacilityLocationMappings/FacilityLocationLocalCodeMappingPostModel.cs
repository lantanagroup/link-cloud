namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;

public class FacilityLocationLocalCodeMappingPostModel
{
    public string LocationId { get; set; } = "";
    public string LocalCodeSystem { get; set; } = "";
    public string LocalCode { get; set; } = "";
    public Guid? HSLOCId { get; set; }
}