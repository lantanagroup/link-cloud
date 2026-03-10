using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Models;

[ExcludeFromCodeCoverage]
public class OrganizationLocationMappingSearchParameters
{
    public int? LocationMappingId { get; set; }
    public string? LocationId { get; set; }
    public bool? IsOrgLocation { get; set; }
    public bool? IsActive { get; set; }
}
