using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class EncounterLocationModel
{
    public int EncounterLocationId { get; set; }
    public int EncounterMappingId { get; set; }
    public int OrganizationLocationMappingId { get; set; }
    public string? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAlias { get; set; }
    public string? PartOfValue { get; set; }
    public bool IsOrgLocation { get; set; }    
    public DateTime CreateDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}
