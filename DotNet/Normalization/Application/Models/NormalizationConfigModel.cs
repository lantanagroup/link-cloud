using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models;

[DataContract]
public class NormalizationConfigModel
{
    [DataMember]
    public string FacilityId { get; set; }
    [DataMember]
    public Dictionary<string, INormalizationOperation> OperationSequence { get; set; }
}
