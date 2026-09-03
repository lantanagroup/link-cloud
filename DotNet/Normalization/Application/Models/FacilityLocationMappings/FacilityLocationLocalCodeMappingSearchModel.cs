namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;

public class FacilityLocationLocalCodeMappingSearchModel
{
    public string? Id { get; set; }
    public string? FacilityId { get; set; }
    public string? LocationId { get; set; }
    public string? LocalCodeSystem { get; set; }
    public string? LocalCode { get; set; }
    public Guid? HSLOCId { get; set; }
    public bool? Unmapped { get; set; }
    public int? PageSize { get; set; }
    public int? PageNumber { get; set; }
}