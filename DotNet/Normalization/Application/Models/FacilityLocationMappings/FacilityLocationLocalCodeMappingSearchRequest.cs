using System.Runtime.Serialization;
using LantanaGroup.Link.Shared.Application.Models.Requests;

namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;

[DataContract]
public class FacilityLocationLocalCodeMappingSearchRequest : PagingRequest
{
    [DataMember]
    public string? Id { get; set; }
    [DataMember]
    public string? FacilityId { get; set; }
    [DataMember]
    public string? LocationId { get; set; }
    [DataMember]
    public string? LocalCodeSystem { get; set; }
    [DataMember]
    public string? LocalCode { get; set; }
    [DataMember]
    public Guid? HSLOCId { get; set; }
    [DataMember]
    public bool? Unmapped { get; set; }
}