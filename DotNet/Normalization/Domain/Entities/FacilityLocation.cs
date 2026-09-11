using LantanaGroup.Link.Shared.Domain.Entities;
namespace LantanaGroup.Link.Normalization.Domain.Entities;

public class FacilityLocation : BaseEntityExtended
{
    public string FacilityId { get; set; } = "";
    public string LocationId { get; set; } = "";
    public string? PartOfId { get; set; }
    public string? ParentFacilityLocationId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAlias { get; set; }
    public virtual FacilityLocation? ParentFacilityLocation { get; set; }
    public virtual ICollection<FacilityLocationLocalCodeMapping>? FacilityLocationLocalCodeMappings { get; set; }
}