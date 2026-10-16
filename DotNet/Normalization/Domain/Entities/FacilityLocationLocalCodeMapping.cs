using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

public class FacilityLocationLocalCodeMapping : BaseEntityExtended
{
    public string FacilityLocationId { get; set; } = "";
    public string LocalCodeSystem { get; set; } = "";
    public string LocalCode { get; set; } = "";
    public Guid? HSLOCId { get; set; }
    public virtual FacilityLocation? FacilityLocation { get; set; }
    public virtual HSLOC? HSLOC { get; set; }
}