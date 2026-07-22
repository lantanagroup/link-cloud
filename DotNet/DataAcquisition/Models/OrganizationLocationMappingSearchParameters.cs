using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Models;

[ExcludeFromCodeCoverage]
[DataContract]
public class OrganizationLocationMappingSearchParameters
{
    [DataMember(IsRequired = false)]
    public int? LocationMappingId { get; set; }
    [DataMember(IsRequired = false)]
    public string? LocationId { get; set; }
    [DataMember(IsRequired = false)]
    public string? LocationName { get; set; }
    [DataMember(IsRequired = false)]
    public string? LocationAlias { get; set; }
    [DataMember(IsRequired = false)]
    public string? PartOfValue { get; set; }
    [DataMember(IsRequired = false)]
    public bool? IsOrgLocation { get; set; }
    [DataMember(IsRequired = false)]
    public bool? IsActive { get; set; }
}
