using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Models;

[ExcludeFromCodeCoverage]
[DataContract]
public class OrganizationLocationConfigurationSearchParameters
{
    [DataMember(IsRequired = false)]
    public int? ConfigId { get; set; }
    [DataMember(IsRequired = false)]
    public bool? IsActive { get; set; }
    [DataMember(IsRequired = false)]
    public string? DescriptionContains { get; set; }
}
