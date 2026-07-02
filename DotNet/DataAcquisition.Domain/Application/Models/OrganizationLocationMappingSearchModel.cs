using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class OrganizationLocationMappingSearchModel
{
    public required string FacilityId { get; set; }
    public string? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAlias { get; set; }
    public string? PartOfValue { get; set; }
    public bool? IsOrgLocation { get; set; }
    public bool? IsActive { get; set; }
}
