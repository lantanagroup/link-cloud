using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;

[DataContract]
public class FacilityLocationLocalCodeMappingModel
{
    [DataMember]
    public string Id { get; set; } = "";
    [DataMember]
    public string FacilityId { get; set; } = "";
    [DataMember]
    public string LocationId { get; set; } = "";
    [DataMember]
    public string? LocationName { get; set; }
    [DataMember]
    public string? LocationAlias { get; set; }
    [DataMember]
    public string LocalCodeSystem { get; set; } = "";
    [DataMember]
    public string LocalCode { get; set; } = "";
    [DataMember]
    public Guid? HSLOCId { get; set; }
    [DataMember]
    public string? HSLOCCode { get; set; }
    [DataMember]
    public string? HSLOCVersion { get; set; }
    [DataMember]
    public DateTime CreateDate { get; set; }
    [DataMember]
    public DateTime? ModifyDate { get; set; }
}