using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;

[DataContract]
public class FacilityLocationLocalCodeMappingPutModel
{
    [DataMember]
    public string LocalCodeSystem { get; set; } = "";
    [DataMember]
    public string LocalCode { get; set; } = "";
    [DataMember]
    public Guid? HSLOCId { get; set; }
}