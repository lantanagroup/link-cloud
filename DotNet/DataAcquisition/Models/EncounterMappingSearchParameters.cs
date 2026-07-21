using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Models;

[ExcludeFromCodeCoverage]
[DataContract]
public class EncounterMappingSearchParameters
{
    [DataMember(IsRequired = false)]
    public string? PatientId { get; set; }
    [DataMember(IsRequired = false)]
    public string? EncounterId { get; set; }
    [DataMember(IsRequired = false)]
    public bool? MappedToOrg { get; set; }
}
