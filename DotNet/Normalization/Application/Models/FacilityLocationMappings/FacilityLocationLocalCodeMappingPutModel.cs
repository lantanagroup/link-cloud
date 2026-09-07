namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;

public class FacilityLocationLocalCodeMappingPutModel
{
    public string LocalCodeSystem { get; set; } = "";
    public string LocalCode { get; set; } = "";
    public Guid? HSLOCId { get; set; }
}