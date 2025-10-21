using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;

[DataContract]
public class FacilityLocationLocalCodeMappingPostModel
{
    [DataMember]
    public string LocationId { get; set; } = "";
    [DataMember]
    public string LocalCodeSystem { get; set; } = "";
    [DataMember]
    public string LocalCode { get; set; } = "";
    [DataMember]
    public Guid? HSLOCId { get; set; }
}