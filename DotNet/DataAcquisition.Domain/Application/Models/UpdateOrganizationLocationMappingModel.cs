using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class UpdateOrganizationLocationMappingModel
{
    public string? LocationName { get; set; }       
    public string? LocationAlias { get; set; }       
    public string? PartOfValue { get; set; }         
    public int? PartOfId { get; set; }              
    public bool? IsOrgLocation { get; set; } 
    public bool? IsActive { get; set; }    
}
