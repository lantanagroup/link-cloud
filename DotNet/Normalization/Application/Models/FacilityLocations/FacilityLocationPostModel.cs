using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocations;

[DataContract]
public class FacilityLocationPostModel
{
    [DataMember]
    public string LocationId { get; set; } = "";
    
    [DataMember]
    public string? PartOfId { get; set; }
    
    [DataMember]
    public string? LocationName { get; set; }
    
    [DataMember]
    public string? LocationAlias { get; set; }
}