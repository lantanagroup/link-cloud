namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;

public class FacilityLocationLocalCodeMappingModel
{
    public string Id { get; set; } = "";
    public string FacilityId { get; set; } = "";
    public string LocationId { get; set; } = "";
    public string? LocationName { get; set; }
    public string? LocationAlias { get; set; }
    public string LocalCodeSystem { get; set; } = "";
    public string LocalCode { get; set; } = "";
    public Guid? HSLOCId { get; set; }
    public string? HSLOCCode { get; set; }
    public string? HSLOCVersion { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
}