using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocations;

[DataContract]
public class FacilityLocationModel
{
    [DataMember]
    public string Id { get; set; } = "";
    [DataMember]
    public string FacilityId { get; set; } = "";
    [DataMember]
    public string LocationId { get; set; } = "";
    [DataMember]
    public string? PartOfId { get; set; }
    [DataMember]
    public string? LocationName { get; set; }
    [DataMember]
    public string? LocationAlias { get; set; }
    [DataMember]
    public DateTime CreateDate { get; set; }
    [DataMember]
    public DateTime? ModifyDate { get; set; }
}